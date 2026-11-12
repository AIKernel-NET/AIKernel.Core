"""A managed build must not allow a Kernel package missing either native RID."""
from pathlib import Path
import shutil
import subprocess
import tempfile

repo = Path(__file__).resolve().parents[3]
artifacts = repo / 'artifacts'
artifacts.mkdir(exist_ok=True)
for rid, filename in (('win-x64', 'aikernel_cuda_driver.dll'), ('linux-x64', 'libaikernel_cuda_driver.so')):
    with tempfile.TemporaryDirectory(prefix='cuda-pack-guard-', dir=artifacts) as temporary:
        stage = Path(temporary).resolve()
        assert stage.is_relative_to(artifacts.resolve())
        (stage/rid).mkdir()
        shutil.copyfile(artifacts/'cuda-native'/rid/filename, stage/rid/filename)
        output = stage/'packages'
        result = subprocess.run(['dotnet','pack',str(repo/'src/AIKernel.Kernel/AIKernel.Kernel.csproj'),
            '-c','Release','--no-build','--no-restore',f'-p:CudaNativeArtifactsRoot={stage}',
            '-o',str(output)],capture_output=True,text=True,encoding='utf-8',errors='replace')
        assert result.returncode != 0 and 'CUDA_NATIVE_ASSET_MISSING' in result.stdout+result.stderr, result.stdout+result.stderr
        assert not list(output.glob('*.nupkg')), 'Incomplete package produced'
        print(f'pack with only {rid}: rejected')
