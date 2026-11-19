"""Render saved observations only; run alongside summary.json with matplotlib."""
from pathlib import Path
import json
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import Patch

r = Path(__file__).parent
data = json.loads((r / 'summary.json').read_text())
rows = data['rows']
baseline = rows[0]['before']['medianSeconds']
fig, ax = plt.subplots(figsize=(14, 9.2), dpi=160)
fig.subplots_adjust(left=.31, right=.97, top=.84, bottom=.19)
fig.suptitle('Gemma 4 E2B: vectorized Q6_K decode and Half lookup',
             x=.05, y=.965, ha='left', fontsize=18, weight='bold')
fig.text(.05, .915, '23 prompt + 128 fixed output tokens | Median of 3 processes | Lower is better', fontsize=11)
for i, row in enumerate(rows):
    for phase, offset, color in [('before', -.18, '#9ca4ae'), ('after', .18, '#168a72')]:
        d = row[phase]; y = i + offset; value = d['medianSeconds']
        ax.barh(y, value, height=.31, color=color)
        ax.errorbar(value, y, xerr=[[value-d['minSeconds']], [d['maxSeconds']-value]],
                    color='#28313a', capsize=2, linewidth=1)
        ax.text(d['maxSeconds']+1.3, y, f'{value:.2f} s / {100*value/baseline:.1f}%',
                va='center', fontsize=9.5)
ax.set_yticks(range(len(rows)), [x['label'] for x in rows], fontsize=10.5)
ax.invert_yaxis(); ax.set_xlim(0, 190)
ax.set_xlabel('Prefill + decode time (seconds)', labelpad=10)
ax.xaxis.grid(True, alpha=.18); ax.set_axisbelow(True)
ax.axvline(baseline, linestyle=':', color='#888f98', linewidth=1)
for name in ['top', 'right', 'left']: ax.spines[name].set_visible(False)
ax.tick_params(axis='y', length=0, pad=8)
fig.legend(handles=[Patch(color='#9ca4ae', label='Before (bebeafd)'),
                    Patch(color='#168a72', label='After (same FP32 order)')],
           loc='lower left', bbox_to_anchor=(.045, .105), ncol=2, frameon=False, fontsize=10)
fig.text(.05, .078, f'100% = Development AVX2 before ({baseline:.2f} s). Whiskers: observed min-max, not confidence intervals.', fontsize=10)
fig.text(.05, .045, 'Development: Xeon E-2174G, 4 workers. Server: EPYC 9555, 6 workers, 7 GB RSS budget. GPU controls unchanged.', fontsize=9.5)
fig.savefig(r/'response-time.png'); fig.savefig(r/'response-time.svg')
p = r/'response-time.svg'
p.write_text('\n'.join(line.rstrip() for line in p.read_text(encoding='utf8').splitlines())+'\n', encoding='utf8')
plt.close(fig)
