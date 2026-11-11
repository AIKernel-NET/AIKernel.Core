from pathlib import Path
import os
import subprocess
import sys

from setuptools import Distribution, setup
from setuptools.command.build_py import build_py
from wheel.bdist_wheel import bdist_wheel


class BinaryDistribution(Distribution):
    def has_ext_modules(self):
        return True


class PlatformWheel(bdist_wheel):
    def get_tag(self):
        _, _, platform = super().get_tag()
        return "py3", "none", platform


class BuildNative(build_py):
    def run(self):
        super().run()
        source = Path(__file__).resolve().parent
        build = self.get_finalized_command("build")
        temporary = Path(build.build_temp).resolve() / "cuda-driver"
        destination = Path(self.build_lib).resolve()
        cmake = os.environ.get("CMAKE", "cmake")
        command = [cmake, "-S", str(source), "-B", str(temporary), "-DCMAKE_BUILD_TYPE=Release"]
        if sys.platform == "win32" and "CMAKE_GENERATOR" not in os.environ:
            command.extend(["-A", "x64"])
        subprocess.run(command, check=True)
        subprocess.run([cmake, "--build", str(temporary), "--config", "Release"], check=True)
        subprocess.run([cmake, "--install", str(temporary), "--config", "Release",
                        "--prefix", str(destination)], check=True)


setup(distclass=BinaryDistribution, cmdclass={"build_py": BuildNative, "bdist_wheel": PlatformWheel})
