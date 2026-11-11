namespace AIKernel.Kernel.Memory;

internal static class CudaMatrixKernel
{
    internal const int Threads = 128;

    // One CUDA block computes one output row. Threads sum strided products, then reduce in shared memory.
    internal const string Ptx = """
        .version 6.4
        .target sm_50
        .address_size 64

        .visible .entry matvec(
            .param .u64 weights, .param .u64 input, .param .u64 output, .param .u32 columns
        )
        {
            .reg .pred %p;
            .reg .u32 %lane, %row, %n, %i, %stride, %partner;
            .reg .u64 %w, %x, %y, %rowOffset, %offset, %wp, %xp, %yp, %sharedBase, %sharedIndex, %sharedAddress, %otherAddress;
            .reg .f32 %sum, %a, %b;
            .shared .align 4 .b8 partial[512];

            ld.param.u64 %w, [weights];
            ld.param.u64 %x, [input];
            ld.param.u64 %y, [output];
            ld.param.u32 %n, [columns];
            mov.u32 %lane, %tid.x;
            mov.u32 %row, %ctaid.x;
            mul.wide.u32 %rowOffset, %row, %n;
            shl.b64 %rowOffset, %rowOffset, 2;
            add.u64 %w, %w, %rowOffset;
            mov.u32 %i, %lane;
            mov.f32 %sum, 0f00000000;
        DOT:
            setp.ge.u32 %p, %i, %n;
            @%p bra STORE;
            mul.wide.u32 %offset, %i, 4;
            add.u64 %wp, %w, %offset;
            add.u64 %xp, %x, %offset;
            ld.global.f32 %a, [%wp];
            ld.global.f32 %b, [%xp];
            fma.rn.f32 %sum, %a, %b, %sum;
            add.u32 %i, %i, 128;
            bra DOT;
        STORE:
            shl.b32 %i, %lane, 2;
            mov.u64 %sharedBase, partial;
            cvt.u64.u32 %sharedIndex, %i;
            add.u64 %sharedAddress, %sharedBase, %sharedIndex;
            st.shared.f32 [%sharedAddress], %sum;
            mov.u32 %stride, 64;
        REDUCE:
            bar.sync 0;
            setp.ge.u32 %p, %lane, %stride;
            @%p bra SKIP;
            shl.b32 %partner, %stride, 2;
            add.u32 %partner, %partner, %i;
            cvt.u64.u32 %sharedIndex, %partner;
            add.u64 %otherAddress, %sharedBase, %sharedIndex;
            ld.shared.f32 %b, [%otherAddress];
            add.f32 %sum, %sum, %b;
            st.shared.f32 [%sharedAddress], %sum;
        SKIP:
            shr.u32 %stride, %stride, 1;
            setp.ne.u32 %p, %stride, 0;
            @%p bra REDUCE;
            setp.ne.u32 %p, %lane, 0;
            @%p ret;
            mul.wide.u32 %offset, %row, 4;
            add.u64 %yp, %y, %offset;
            st.global.f32 [%yp], %sum;
            ret;
        }
        """;
}
