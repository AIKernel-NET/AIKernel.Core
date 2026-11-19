"""Validate actual native images and dependency boundaries in the Kernel NuGet package."""
import argparse
import struct
import xml.etree.ElementTree as ET
import zipfile

def verify(path):
    with zipfile.ZipFile(path) as package:
        names = package.namelist()
        assert len(names) == len(set(names)), "duplicate package entries"
        dll = package.read("runtimes/win-x64/native/aikernel_cuda_driver.dll")
        so = package.read("runtimes/linux-x64/native/libaikernel_cuda_driver.so")
        assert dll[:2] == b"MZ", "Windows asset is not PE"
        pe = struct.unpack_from("<I", dll, 0x3c)[0]
        assert dll[pe:pe+4] == b"PE\0\0" and struct.unpack_from("<H", dll, pe+4)[0] == 0x8664, "not x64 PE"
        assert so[:6] == b"\x7fELF\x02\x01" and struct.unpack_from("<H", so, 18)[0] == 62, "not x64 ELF"
        assert package.read("lib/net10.0/AIKernel.Kernel.dll")[:2] == b"MZ"
        assert package.read("licenses/cuda-driver/LICENSE")
        assert not any("torch" in name.lower() for name in names), "Torch artifact included"
        spec = ET.fromstring(package.read(next(n for n in names if n.endswith('.nuspec'))))
        for dependency in spec.iter():
            if dependency.tag.rsplit('}',1)[-1] == 'dependency':
                name = dependency.attrib['id'].lower()
                assert 'torch' not in name and not name.startswith('aikernel.cuda13'), name
        print(f"cuda-driver-nupkg: ok; win-x64={len(dll)} bytes; linux-x64={len(so)} bytes")

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('package')
    verify(parser.parse_args().package)
