"""Render measured Gemma 4 response times. No inferred GPU-before or theoretical bandwidth numbers."""
from pathlib import Path
import argparse,json,statistics,hashlib,shutil,xml.etree.ElementTree as ET

def main():
 parser=argparse.ArgumentParser(description=__doc__)
 parser.add_argument('measurements',type=Path);parser.add_argument('destination',type=Path)
 args=parser.parse_args();root=args.measurements;out=args.destination;out.mkdir(parents=True,exist_ok=True)
 cases=[('Development CPU · reference',root,'bench-before',4,'#94a3b8'),
        ('Development CPU · SIMD',root,'bench-cpu',4,'#10b981'),
        ('RTX 4000 SFF Ada · CUDA',root,'bench-cuda-ada',4,'#f59e0b'),
        ('T1000 · CUDA',root,'bench-cuda-t1000',4,'#fbbf24'),
        ('Server CPU · reference',root/'server','bench-before',6,'#94a3b8'),
        ('Server CPU · SIMD + SSD backing',root/'server','paged-cpu',6,'#3b82f6')]
 rows=[];raw=out/'raw';raw.mkdir(exist_ok=True)
 for name,folder,prefix,threads,color in cases:
  records=[]
  for i in range(3):
   p=folder/f'{prefix}-{i}.json';data=json.loads(p.read_text());result=data['results'][0]
   assert data['promptTokens']==23 and result['outputTokens']==128 and result['decodeForwardTokens']==127
   assert data['threads']==threads
   records.append(result)
   shutil.copyfile(p,raw/(('server-' if folder.name=='server' else '')+p.name))
   gate=folder/f'gate-{prefix.removeprefix("bench-")}-{i}.json'
   if folder==root:
    g=json.loads(gate.read_text());assert max(g['cpuPercent'])<=10;shutil.copyfile(gate,raw/gate.name)
  total=[x['totalMs']/1000 for x in records]
  rows.append({'name':name,'seconds':statistics.median(total),'minSeconds':min(total),'maxSeconds':max(total),
    'prefillSeconds':statistics.median(x['prefillMs']/1000 for x in records),
    'decodeMsPerToken':statistics.median(x['decodeMsPerToken'] for x in records),
    'hashes':[x['hash'] for x in records],'threads':threads,'color':color})
 baseline=rows[0]['seconds']
 for row in rows:row['percentOfDevelopmentReference']=100*row['seconds']/baseline
 memory=[]
 for name in ['paged-context-2048','paged-all-ple-pages']:
  data=json.loads((root/'server'/f'{name}.json').read_text())
  peak=max(data['externalPeakRssBytes'],data['peakWorkingSetBytes'])
  assert peak<7_000_000_000
  memory.append({'test':name,'peakBytes':peak,'promptTokens':data['promptTokens'],'outputTokens':128,'statistics':data['MemoryStatistics']})
  shutil.copyfile(root/'server'/f'{name}.json',raw/f'server-{name}.json')
 summary={'model':'Gemma 4 E2B QAT Q4_0','modelSha256':'fa401b55b07ee70a54c6dae3903c783a6e65064312529ea57175cb5f8dec6634',
  'method':'23 prompt tokens + 128 fixed greedy output tokens (127 decode forwards), one unmeasured forward, three independent processes, median; profile off; model loading, workspace allocation/cleanup and HTTP excluded.',
  'note':'CUDA is newly implemented; its comparison baseline is CPU reference, not a previous CUDA implementation. Development and Server differ in hardware and worker count.',
  'rows':rows,'memory':memory,'cpuReductionPercent':100*(1-rows[1]['seconds']/rows[0]['seconds']),
  'serverReductionPercent':100*(1-rows[5]['seconds']/rows[4]['seconds'])}
 summary['allBenchmarkTokensEqual']=len({h for row in rows for h in row['hashes']})==1
 numerical=[]
 for name in ['cpu-numeric','gpu-numeric','t1000-numeric','paged-numeric','server-numeric','server-paged-numeric']:
  p=root/(name+'.json');data=json.loads(p.read_text());assert data['pass'] and data['tokensEqual']
  numerical.append({'path':name,'comparisons':len(data['comparisons']),'maximumAbsoluteError':max(x['maximumAbsoluteError'] for x in data['comparisons']),
   'maximumLogitError':max(x['maximumAbsoluteError'] for x in data['comparisons'] if x['name'].startswith('logits-')),
   'maximumRmsError':max(x['rootMeanSquareError'] for x in data['comparisons']),'tokensEqual':True})
  shutil.copyfile(p,out/p.name)
 summary['numerical']=numerical
 for name in ['bonsai-fp32.json','bonsai-gpu-fp32.json','bonsai-hashes.json','paging-equivalence.json','gpu-matrices-ada.json','gpu-matrices-t1000.json','gpu-lifecycle.json','native-linux-build.json']:
  shutil.copyfile(root/name,out/name)
 for name in ['paged-lifecycle.json','paged-lifecycle-time.txt','model-verified.json']:
  if (root/'server'/name).exists():shutil.copyfile(root/'server'/name,out/('server-'+name))
 for pattern in ['bench-cpu-*.json','profile-*.json','*-input.json']:
  for path in (root/'server').glob(pattern):shutil.copyfile(path,raw/('server-'+path.name))
 for path,name in [(root/'local-environment.json','development-environment.json'),(root/'server/environment.json','server-environment.json')]:
  shutil.copyfile(path,out/name)
 counters=ET.parse(root/'tests/gemma4-final.trx').find('.//{*}Counters').attrib
 assert int(counters['failed'])==0 and counters['passed']==counters['total']
 summary['unitTests']={k:int(v) for k,v in counters.items()}
 (out/'unit-tests.json').write_text(json.dumps(summary['unitTests'],indent=2))
 source_hash=json.loads((root/'local-model-hash.json').read_text(encoding='utf-8-sig'))['Hash'].lower()
 assert source_hash==summary['modelSha256']
 artifacts={}
 for path in [root/'app-v2/AIKernel.Kernel.dll',root/'app-v2/aikernel_cuda_driver.dll',root/'app-v2/aikernel_cpu_accel.dll',root/'libaikernel_cuda_driver.so',root/'tests/gemma4-final.trx']:
  artifacts[path.name]={'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'bytes':path.stat().st_size}
 final_dll=root/'app-final/AIKernel.Kernel.dll'
 artifacts['AIKernel.Kernel.final.dll']={'sha256':hashlib.sha256(final_dll.read_bytes()).hexdigest(),'bytes':final_dll.stat().st_size,
  'note':'Post-measurement change only disposes an empty paging arena when constructor RSS admission fails; numerical kernels are unchanged.'}
 (out/'artifact-hashes.json').write_text(json.dumps(artifacts,indent=2))
 (out/'comparison.json').write_text(json.dumps(summary,indent=2,ensure_ascii=False),encoding='utf-8')
 import matplotlib
 matplotlib.use('Agg')
 import matplotlib.pyplot as plt
 fig,ax=plt.subplots(figsize=(12.8,6.8));fig.subplots_adjust(left=.29,right=.97,top=.79,bottom=.23)
 ys=list(range(len(rows)))
 for y,row in zip(ys,rows):
  ax.barh(y,row['seconds'],color=row['color'],height=.6)
  ax.errorbar(row['seconds'],y,xerr=[[row['seconds']-row['minSeconds']],[row['maxSeconds']-row['seconds']]],fmt='none',ecolor='#334155',capsize=3)
  ax.text(row['maxSeconds']+baseline*.018,y,f"{row['seconds']:.2f} s  |  {row['percentOfDevelopmentReference']:.1f}%",va='center',fontsize=10,color='#0f172a')
 ax.set_yticks(ys,[r['name'] for r in rows],fontsize=10);ax.invert_yaxis();ax.set_xlim(0,baseline*1.42)
 ax.set_xlabel('Prefill + decode (seconds; model loading excluded)',fontsize=11,labelpad=12)
 ax.xaxis.grid(True,color='#e2e8f0');ax.set_axisbelow(True)
 for side in ['top','right','left']:ax.spines[side].set_visible(False)
 ax.spines['bottom'].set_color('#cbd5e1');ax.tick_params(axis='y',length=0,pad=12)
 fig.text(.04,.94,'Gemma 4 E2B: CPU / GPU response time',fontsize=21,weight='bold',color='#0f172a')
 fig.text(.04,.88,'128 fixed output tokens · same QAT weights / FP32 activations · median of 3 runs',fontsize=11,color='#475569')
 fig.text(.04,.06,'100% = Development CPU reference. Whiskers = observed min/max.\nDevelopment: Xeon E-2174G, 4 workers. Server: EPYC 9555, 6 workers; AIKernel RAM / SSD paging enabled.',fontsize=10,color='#475569',linespacing=1.6)
 fig.savefig(out/'response-time.png',dpi=160);fig.savefig(out/'response-time.svg');plt.close(fig)
 svg=out/'response-time.svg'
 svg.write_text('\n'.join(line.rstrip() for line in svg.read_text(encoding='utf-8').splitlines())+'\n',encoding='utf-8')
 report=['# Gemma 4 E2BのCPU／GPU最適化とServerの7GB検証','',
  '2026-11-15〜16の検証。基準は `5747fae` の参照演算です。比較用の `ReferenceCpuMatrix` とモデルの演算グラフを保ち、同じ実行器で参照／SIMD／CUDAを切り替えて測定しました。旧バイナリをそのまま計測した比較ではありません。',
  '', '## 応答時間', '',
  '同一QAT重み、入力23トークン、greedyで出力128トークン固定（EOSを無視、decode forwardは127回）。1 forwardのウォームアップ後に各経路を独立プロセスで3回測定し、中央値を採用しました。値はprefill＋decodeで、モデル読み込み・作業領域の確保／消去・HTTP往復は含みません。', '',
  '| 経路 | 処理時間（秒） | 観測範囲（秒） | Decode（ms/token） | 開発CPU参照比 |',
  '| --- | ---: | ---: | ---: | ---: |']
 for row in rows:report.append(f"| {row['name']} | {row['seconds']:.2f} | {row['minSeconds']:.2f}–{row['maxSeconds']:.2f} | {row['decodeMsPerToken']:.1f} | {row['percentOfDevelopmentReference']:.1f}% |")
 report+=['',f"CPUの短縮率は開発機で **{summary['cpuReductionPercent']:.1f}%**、ServerのRAM予算付きSSDバックエンドで **{summary['serverReductionPercent']:.1f}%** でした。CUDAは今回の新設経路です。GPUの改修前の実測値は存在せず、CPU参照を比較基準にしています。",'',
  '![応答時間の比較](evidence/gemma4-optimization-20261115/response-time.png)','',
  '開発機はXeon E-2174G、4ワーカー。開始前にCPU使用率を1秒間隔で3回確認し、すべて10%以下のときだけ開始しました。WSL停止前の初期記録は比較から除外しています。ただしWSL補助VMの再起動とバックグラウンド負荷があり、測定中の負荷を完全には隔離できていません。観測範囲を併記し、少数回の差を普遍的な性能保証とは扱いません。ServerはEPYC 9555、同じ6 CPUへのaffinityと6ワーカーを固定しました。共有ホストの他利用者の負荷は分離していません。','',
  '通常のmapped SIMD経路ではServerの中央値は42.57秒でした。RAM予算付き経路の53.64秒にはページ管理・分割実行の追加コストがあります。現在のモデルでは固定4MiBページの余白もRSSを増やします。RAMの使用量を指定できることと、速度・メモリ効率は別に評価しています。','',
  '## 7GBとSSDの扱い','',
  '7GBは十進の7,000,000,000バイトです。`Gemma4MemoryOptions` でRSS上限7GB、重みキャッシュ5GB、ランタイム等の余裕1GBを指定しました。重みの無制限mmapを解放し、AIKernelの `VirtualMemorySpace` が行境界に合わせた4MiBページをRAM／GGUF backing間で管理します。不変の重みを重複swapへ書く必要はありません。KV・活性化は文脈上限内のRAMで管理し、終了時に消去します。','',
  '| 試験 | ピークRSS（GB） | 内容 |','| --- | ---: | --- |']
 descriptions=['入力1920＋生成128。局所KVの512境界を越えて2048枠を確認。','入力483＋生成128。全PLEページを読むtoken ID列を使い、重みキャッシュの最大使用側を確認。']
 for item,description in zip(memory,descriptions):report.append(f"| {item['test']} | {item['peakBytes']/1e9:.3f} | {description} |")
 report+=['', 'RSSはLinuxの子プロセス `ru_maxrss`、`/proc` の高水位、.NETのピーク値を取得し、表には大きい値を使用しました。試験ハーネスは7GB超過時に対象プロセスを停止します。エンジンはRSSと余裕を確認してキャッシュを解放し、非退避メモリだけで余裕を確保できなければ例外で停止します。OSの強制メモリ制限ではなく、OS全体の共有ファイルキャッシュもRSSには含みません。', '',
  'キャッシュを32MiBへ意図的に縮める試験では、RAMは8ページ、退避済みbackingは532ページ、読み込みは16,213回になりました。Windowsではmapped SIMDとの229配列がビット一致し、Serverでも公式参照の228項目を通過しました。`SwapReads=0` は退避が無かった意味ではなく、不変GGUFを再利用するためです。再読み込みは `BackingReads` に記録します。物理SSD I/OとOSキャッシュからの読み込みの区別はこのカウンターではできません。', '',
  '今回の実モデルは3,349,516,256バイトです。12GB級モデル全体の実行を証明する試験ではありません。', '',
  '## 数値と回帰','',
  '公式参照は[revision固定のTransformers Gemma4TextModel](https://github.com/huggingface/transformers/blob/3a134bc37f7c22c801b618e289b0255425f22647/src/transformers/models/gemma4/modeling_gemma4.py)です。同じGoogle QAT GGUFを独立デコーダーで読み込んだFP32グラフを使い、元BF16版との量子化品質比較とは分けています。中間値・ロジットの最大絶対誤差≤1e-3、RMS≤1e-4を維持し、12ステップのtoken ID列は全経路で一致しました。', '',
  '| 経路 | 比較項目 | 最大ロジット誤差 | 最大中間値誤差 |','| --- | ---: | ---: | ---: |']
 for x in numerical:report.append(f"| {x['path']} | {x['comparisons']} | {x['maximumLogitError']:.3g} | {x['maximumAbsoluteError']:.3g} |")
 report+=['',f"128トークン固定の今回のベンチマーク入力でも、全経路のtoken IDハッシュは一致しました: `{rows[0]['hashes'][0]}`。" if summary['allBenchmarkTokensEqual'] else '128トークンの経路間差はcomparison.jsonのhashesを参照してください。','',
  '- Bonsai 1.7B／4B: CPU FP32とCUDA FP32の各910,014ロジットが基準とビット一致。CPU／CUDA Q8の10経路でも128トークンの基準ハッシュが一致。',
  f"- 単体テスト{counters['passed']}件成功。新CUDAの独立倍精度オラクルとの比較は各GPUで30ケース成功。",
  '- 公開Generate APIで日本語返信、キャンセル後の再生成、Dispose後の拒否を確認。GPU scratch消去失敗は呼び出し元へ報告。', '',
  '## 改修内容と残る範囲','',
  '- CPU: Q4_0をレジスター内で展開し、4本のFP32アキュムレーターへ蓄積。F16／Q6_KにもSIMD内積を使用。追加の活性化量子化は行わない。',
  '- CUDA: F16／Q4_0／Q6_Kの符号化重み1,405,796,352バイトを保持。4行／128スレッドで1行を1warpが処理し、GPU内で展開・内積・リダクションする。PLE検索表はCPU側。',
  '- Server: 予算付き重みページングと、VirtualMemorySpaceのRAM解放APIを追加。ページ境界で借りた領域の寿命を固定し、並列計算中は退避しない。',
  '- 新CUDAはWindowsのAda／T1000で実行。LinuxはGCC 8.5、-O3、警告をエラーとしてビルドし、新APIのexportまで確認。Linux GPU実行は今回の測定対象外。',
  '- Attention／RoPE／GELU／KVはCPU側。GPU全forward常駐化、バッチprefill、Gemma用AVX-512／VNNIは今後の対象。Bonsaiの既存auto選択は変更していない。', '',
  'Serverの12生成トークン診断では行列演算区間が参照経路の99.3%、mapped SIMDの94.4%でした。診断の計測あり／なし差はそれぞれ+1.23%／−0.85%で、各1回のため有意な差とはしません。上表の性能測定は区間計測を無効化しています。', '',
  '[APIとメモリ設定](../gemma4-e2b-inference.md) · [数値・実測JSONとハッシュ](evidence/gemma4-optimization-20261115/README.md)']
 (out.parent.parent/'gemma4-optimization-20261115-jp.md').write_text('\n'.join(report)+'\n',encoding='utf-8')
 (out/'README.md').write_text('''# Gemma 4 E2B optimization evidence

See [the report](../../gemma4-optimization-20261115-jp.md) and `comparison.json` for measurement scope, limits and results.
`raw/` contains three observations per plotted execution path and development CPU start gates.
Numerical comparison JSON retains all 228 comparisons per execution path. The original reference provenance is in
[the generation evidence](../gemma4-e2b-20261115/README.md).

Rebuild the managed harness with `dotnet publish tests/AIKernel.Inference.Optimization.Validation -c Release`.
Select `GEMMA_BACKEND=Reference`, `Auto`, or `Cuda`; `GEMMA_GPU=0|1`; `GEMMA_THREADS=4|6`;
`GEMMA_CONTEXT=1024|2048`. For bounded CPU paging set `GEMMA_SWAP_DIRECTORY` to a private directory;
`GEMMA_CACHE_BYTES=5000000000` (normal) or `33554432` (paging pressure). RSS ceiling defaults to 7,000,000,000 bytes.

Run `--gemma-benchmark MODEL INPUT_JSON OUTPUT_JSON 1` three times under the stated CPU start gate.
INPUT_JSON has the `prompt` token IDs from the original generation evidence and `steps: 128`.
For official comparisons use `--gemma-forward MODEL generation-input.json OUTPUT_DIRECTORY`, then `compare_gemma4.py`.
For public API lifecycle checks use `--gemma-lifecycle MODEL`.

CPU tests: `dotnet test tests/AIKernel.Core.Tests -c Release`.
GPU matrix oracle: `python native/cuda/tests/gemma_gpu.py NATIVE_DLL GPU_ORDINAL`.
Rebuild native CUDA using CMake Release. The committed Gemma PTX is generated with
`python native/cuda/build_gemma_ptx.py PATH_TO_NVRTC_LIBRARY` (`compute_75`, `--fmad=false`).
Model weights are not packaged. Match their SHA-256 and native/managed artifact hashes before comparing runs.
The measured assembly is `AIKernel.Kernel.dll` in the artifact manifest. The final assembly adds only
constructor-failure cleanup of the empty paging arena; its separate hash and final test counters are recorded.
''',encoding='utf-8')
 print(json.dumps({k:summary[k] for k in ['cpuReductionPercent','serverReductionPercent','memory']},ensure_ascii=False))

if __name__=='__main__':main()
