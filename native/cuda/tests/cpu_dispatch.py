"""Bounded CPU-only correctness test, including SIMD tails and unsupported ISA.

Run against the exact same Linux .so on AVX2 and AVX-512 machines. Does not
create a CUDA context or load a model. Optional microbenchmark is single-threaded.
"""
import argparse
import ctypes as C
import json
import math
import os
import random
import time


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('library')
    parser.add_argument('--benchmark', action='store_true')
    args = parser.parse_args()
    lib = C.CDLL(os.path.abspath(args.library))
    lib.ak_cpu_gemv_backend.restype = C.c_int32
    lib.ak_gemv.argtypes = [C.c_void_p, C.c_uint64, C.POINTER(C.c_float), C.POINTER(C.c_float), C.c_uint32, C.c_uint32]
    lib.ak_gemv.restype = C.c_int32
    report = {}
    for mode in ['auto', 'scalar', 'avx', 'avx2', 'avx512', 'invalid']:
        rng = random.Random(148512)
        os.environ['AIKERNEL_CPU_GEMV'] = mode
        backend = lib.ak_cpu_gemv_backend()
        if backend == -1:
            a = (C.c_float * 1)(2)
            out = (C.c_float * 1)(-99)
            assert lib.ak_gemv(None, C.addressof(a), a, out, 1, 1) == 1
            assert out[0] == -99
            report[mode] = {'backend': backend, 'unsupported_rejected': True}
            continue
        assert mode != 'invalid'
        if mode != 'auto':
            assert backend == {'scalar': 0, 'avx': 3, 'avx2': 1, 'avx512': 2}[mode]
        count = 0
        max_error = 0
        for cols in [1, 7, 8, 15, 16, 17, 31, 32, 33, 63, 64, 65, 127, 511, 2048]:
            rows = 3
            # Deliberately unaligned pointers, plus negative values and cancellation.
            matrix = (C.c_float * (rows * cols + 1))(*[rng.uniform(-3, 3) for _ in range(rows * cols + 1)])
            vector = (C.c_float * (cols + 1))(*[rng.uniform(-3, 3) for _ in range(cols + 1)])
            out = (C.c_float * rows)()
            x = C.cast(C.byref(vector, 4), C.POINTER(C.c_float))
            status = lib.ak_gemv(None, C.addressof(matrix) + 4, x, out, rows, cols)
            assert status == 0, (mode, status)
            for row in range(rows):
                products = [float(matrix[1 + row * cols + col]) * float(vector[1 + col]) for col in range(cols)]
                expected = math.fsum(products)
                error = abs(out[row] - expected)
                assert error <= 3e-6 * math.fsum(map(abs, products)) + 1e-6, (mode, cols, out[row], expected)
                max_error = max(max_error, error)
                count += 1
            assert lib.ak_gemv(None, C.addressof(matrix), x, out, 0, cols) == 1
        report[mode] = {'backend': backend, 'rows_checked': count, 'max_abs_error': max_error}
        if args.benchmark:
            rows, cols, repeats = 256, 2048, 40
            matrix = (C.c_float * (rows * cols))(*[rng.uniform(-1, 1) for _ in range(rows * cols)])
            vector = (C.c_float * cols)(*[rng.uniform(-1, 1) for _ in range(cols)])
            out = (C.c_float * rows)()
            start = time.perf_counter()
            for _ in range(repeats):
                assert lib.ak_gemv(None, C.addressof(matrix), vector, out, rows, cols) == 0
            report[mode]['microbenchmark_ms'] = (time.perf_counter() - start) * 1000 / repeats
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
