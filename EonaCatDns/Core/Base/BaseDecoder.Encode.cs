using System;

namespace EonaCat.Dns.Core.Base
{
    internal partial class BaseDecoder
    {
        private unsafe void EncodeBase16(ReadOnlySpan<byte> input, Span<byte> output, bool flush, out int inputUsed, out int outputUsed, out bool completed)
        {
            if (input.IsEmpty || output.IsEmpty)
            {
                inputUsed = outputUsed = 0;
                completed = true;
                return;
            }

            // #1: preparing
            const int inputBlockSize = 1;
            const int outputBlockSize = 2;
            const int encodingBits = 4;
            const ulong encodingMask = 15;

            _ = Alphabet.Alphabet ?? throw new InvalidOperationException();

            fixed (char* alphabetPtr = Alphabet.Alphabet)
            fixed (byte* outputBytes = &System.Runtime.InteropServices.MemoryMarshal.GetReference(output))
            fixed (byte* inputBytes = &System.Runtime.InteropServices.MemoryMarshal.GetReference(input))
            {
                var inputPtr = inputBytes;
                var outputPtr = outputBytes;

                // #2: encoding whole blocks

                var wholeBlocksToProcess = Math.Min(input.Length / inputBlockSize, output.Length / outputBlockSize);

                inputUsed = inputBlockSize * wholeBlocksToProcess;
                outputUsed = outputBlockSize * wholeBlocksToProcess;

                while (wholeBlocksToProcess-- > 0)
                {
                    // fill input
                    var inputBlock =
                        (ulong)inputPtr[0] << (8 * 0);

                    // encode input
                    outputPtr[1] = (byte)alphabetPtr[(int)((inputBlock >> encodingBits * 0) & encodingMask)];
                    outputPtr[0] = (byte)alphabetPtr[(int)((inputBlock >> encodingBits * 1) & encodingMask)];

                    inputPtr += 1;
                    outputPtr += 2;
                }

                input = input.Slice(inputUsed);
                output = output.Slice(outputUsed);
            }

            // #3: encoding any final block (partial with padding)

            EncodeAny(input, output, flush, out var finalInputUsed, out var finalOutputUsed, out completed);

            inputUsed += finalInputUsed;
            outputUsed += finalOutputUsed;
        }

        private unsafe void EncodeBase16(ReadOnlySpan<byte> input, Span<char> output, bool flush, out int inputUsed, out int outputUsed, out bool completed)
        {
            if (input.IsEmpty || output.IsEmpty)
            {
                inputUsed = outputUsed = 0;
                completed = true;
                return;
            }

            // #1: preparing
            const int inputBlockSize = 1;
            const int outputBlockSize = 2;
            const int encodingBits = 4;
            const ulong encodingMask = 15;

            _ = Alphabet.Alphabet ?? throw new InvalidOperationException();

            fixed (char* alphabetPtr = Alphabet.Alphabet)
            fixed (char* outputBytes = &System.Runtime.InteropServices.MemoryMarshal.GetReference(output))
            fixed (byte* inputBytes = &System.Runtime.InteropServices.MemoryMarshal.GetReference(input))
            {
                var inputPtr = inputBytes;
                var outputPtr = outputBytes;

                // #2: encoding whole blocks

                var wholeBlocksToProcess = Math.Min(input.Length / inputBlockSize, output.Length / outputBlockSize);

                inputUsed = inputBlockSize * wholeBlocksToProcess;
                outputUsed = outputBlockSize * wholeBlocksToProcess;

                while (wholeBlocksToProcess-- > 0)
                {
                    // fill input
                    var inputBlock =
                        (ulong)inputPtr[0] << (8 * 0);

                    // encode input
                    outputPtr[1] = alphabetPtr[(int)((inputBlock >> encodingBits * 0) & encodingMask)];
                    outputPtr[0] = alphabetPtr[(int)((inputBlock >> encodingBits * 1) & encodingMask)];

                    inputPtr += 1;
                    outputPtr += 2;
                }

                input = input.Slice(inputUsed);
                output = output.Slice(outputUsed);
            }

            // #3: encoding any final block (partial with padding)

            EncodeAny(input, output, flush, out var finalInputUsed, out var finalOutputUsed, out completed);

            inputUsed += finalInputUsed;
            outputUsed += finalOutputUsed;
        }

        private unsafe void EncodeBase32(ReadOnlySpan<byte> input, Span<byte> output, bool flush, out int inputUsed, out int outputUsed, out bool completed)
        {
            if (input.IsEmpty || output.IsEmpty)
            {
                inputUsed = outputUsed = 0;
                completed = true;
                return;
            }

            // #1: preparing
            const int inputBlockSize = 5;
            const int outputBlockSize = 8;
            const int encodingBits = 5;
            const ulong encodingMask = 31;

            _ = Alphabet.Alphabet ?? throw new InvalidOperationException();

            fixed (char* alphabetPtr = Alphabet.Alphabet)
            fixed (byte* outputBytes = &System.Runtime.InteropServices.MemoryMarshal.GetReference(output))
            fixed (byte* inputBytes = &System.Runtime.InteropServices.MemoryMarshal.GetReference(input))
            {
                var inputPtr = inputBytes;
                var outputPtr = outputBytes;

                // #2: encoding whole blocks

                var wholeBlocksToProcess = Math.Min(input.Length / inputBlockSize, output.Length / outputBlockSize);

                inputUsed = inputBlockSize * wholeBlocksToProcess;
                outputUsed = outputBlockSize * wholeBlocksToProcess;

                while (wholeBlocksToProcess-- > 0)
                {
                    // fill input
                    var inputBlock =
                        ((ulong)inputPtr[0] << (8 * 4)) |
                        ((ulong)inputPtr[1] << (8 * 3)) |
                        ((ulong)inputPtr[2] << (8 * 2)) |
                        ((ulong)inputPtr[3] << (8 * 1)) |
                        ((ulong)inputPtr[4] << (8 * 0));

                    // encode input
                    outputPtr[7] = (byte)alphabetPtr[(int)((inputBlock >> encodingBits * 0) & encodingMask)];
                    outputPtr[6] = (byte)alphabetPtr[(int)((inputBlock >> encodingBits * 1) & encodingMask)];
                    outputPtr[5] = (byte)alphabetPtr[(int)((inputBlock >> encodingBits * 2) & encodingMask)];
                    outputPtr[4] = (byte)alphabetPtr[(int)((inputBlock >> encodingBits * 3) & encodingMask)];
                    outputPtr[3] = (byte)alphabetPtr[(int)((inputBlock >> encodingBits * 4) & encodingMask)];
                    outputPtr[2] = (byte)alphabetPtr[(int)((inputBlock >> encodingBits * 5) & encodingMask)];
                    outputPtr[1] = (byte)alphabetPtr[(int)((inputBlock >> encodingBits * 6) & encodingMask)];
                    outputPtr[0] = (byte)alphabetPtr[(int)((inputBlock >> encodingBits * 7) & encodingMask)];

                    inputPtr += 5;
                    outputPtr += 8;
                }

                input = input.Slice(inputUsed);
                output = output.Slice(outputUsed);
            }

            // #3: encoding any final block (partial with padding)

            EncodeAny(input, output, flush, out var finalInputUsed, out var finalOutputUsed, out completed);

            inputUsed += finalInputUsed;
            outputUsed += finalOutputUsed;
        }

        private unsafe void EncodeBase32(ReadOnlySpan<byte> input, Span<char> output, bool flush, out int inputUsed, out int outputUsed, out bool completed)
        {
            if (input.IsEmpty || output.IsEmpty)
            {
                inputUsed = outputUsed = 0;
                completed = true;
                return;
            }

            // #1: preparing
            const int inputBlockSize = 5;
            const int outputBlockSize = 8;
            const int encodingBits = 5;
            const ulong encodingMask = 31;

            _ = Alphabet.Alphabet ?? throw new InvalidOperationException();

            fixed (char* alphabetPtr = Alphabet.Alphabet)
            fixed (char* outputBytes = &System.Runtime.InteropServices.MemoryMarshal.GetReference(output))
            fixed (byte* inputBytes = &System.Runtime.InteropServices.MemoryMarshal.GetReference(input))
            {
                var inputPtr = inputBytes;
                var outputPtr = outputBytes;

                // #2: encoding whole blocks

                var wholeBlocksToProcess = Math.Min(input.Length / inputBlockSize, output.Length / outputBlockSize);

                inputUsed = inputBlockSize * wholeBlocksToProcess;
                outputUsed = outputBlockSize * wholeBlocksToProcess;

                while (wholeBlocksToProcess-- > 0)
                {
                    // fill input
                    var inputBlock =
                        ((ulong)inputPtr[0] << (8 * 4)) |
                        ((ulong)inputPtr[1] << (8 * 3)) |
                        ((ulong)inputPtr[2] << (8 * 2)) |
                        ((ulong)inputPtr[3] << (8 * 1)) |
                        ((ulong)inputPtr[4] << (8 * 0));

                    // encode input
                    outputPtr[7] = alphabetPtr[(int)((inputBlock >> encodingBits * 0) & encodingMask)];
                    outputPtr[6] = alphabetPtr[(int)((inputBlock >> encodingBits * 1) & encodingMask)];
                    outputPtr[5] = alphabetPtr[(int)((inputBlock >> encodingBits * 2) & encodingMask)];
                    outputPtr[4] = alphabetPtr[(int)((inputBlock >> encodingBits * 3) & encodingMask)];
                    outputPtr[3] = alphabetPtr[(int)((inputBlock >> encodingBits * 4) & encodingMask)];
                    outputPtr[2] = alphabetPtr[(int)((inputBlock >> encodingBits * 5) & encodingMask)];
                    outputPtr[1] = alphabetPtr[(int)((inputBlock >> encodingBits * 6) & encodingMask)];
                    outputPtr[0] = alphabetPtr[(int)((inputBlock >> encodingBits * 7) & encodingMask)];

                    inputPtr += 5;
                    outputPtr += 8;
                }

                input = input.Slice(inputUsed);
                output = output.Slice(outputUsed);
            }

            // #3: encoding any final block (partial with padding)

            EncodeAny(input, output, flush, out var finalInputUsed, out var finalOutputUsed, out completed);

            inputUsed += finalInputUsed;
            outputUsed += finalOutputUsed;
        }

        private unsafe void EncodeBase64(ReadOnlySpan<byte> input, Span<byte> output, bool flush, out int inputUsed, out int outputUsed, out bool completed)
        {
            if (input.IsEmpty || output.IsEmpty)
            {
                inputUsed = outputUsed = 0;
                completed = true;
                return;
            }

            // #1: preparing
            const int inputBlockSize = 3;
            const int outputBlockSize = 4;
            const int encodingBits = 6;
            const ulong encodingMask = 63;

            _ = Alphabet.Alphabet ?? throw new InvalidOperationException();

            fixed (char* alphabetPtr = Alphabet.Alphabet)
            fixed (byte* outputBytes = &System.Runtime.InteropServices.MemoryMarshal.GetReference(output))
            fixed (byte* inputBytes = &System.Runtime.InteropServices.MemoryMarshal.GetReference(input))
            {
                var inputPtr = inputBytes;
                var outputPtr = outputBytes;

                // #2: encoding whole blocks

                var wholeBlocksToProcess = Math.Min(input.Length / inputBlockSize, output.Length / outputBlockSize);

                inputUsed = inputBlockSize * wholeBlocksToProcess;
                outputUsed = outputBlockSize * wholeBlocksToProcess;

                while (wholeBlocksToProcess-- > 0)
                {
                    // fill input
                    var inputBlock =
                        ((ulong)inputPtr[0] << (8 * 2)) |
                        ((ulong)inputPtr[1] << (8 * 1)) |
                        ((ulong)inputPtr[2] << (8 * 0));

                    // encode input
                    outputPtr[3] = (byte)alphabetPtr[(int)((inputBlock >> encodingBits * 0) & encodingMask)];
                    outputPtr[2] = (byte)alphabetPtr[(int)((inputBlock >> encodingBits * 1) & encodingMask)];
                    outputPtr[1] = (byte)alphabetPtr[(int)((inputBlock >> encodingBits * 2) & encodingMask)];
                    outputPtr[0] = (byte)alphabetPtr[(int)((inputBlock >> encodingBits * 3) & encodingMask)];

                    inputPtr += 3;
                    outputPtr += 4;
                }

                input = input.Slice(inputUsed);
                output = output.Slice(outputUsed);
            }

            // #3: encoding any final block (partial with padding)

            EncodeAny(input, output, flush, out var finalInputUsed, out var finalOutputUsed, out completed);

            inputUsed += finalInputUsed;
            outputUsed += finalOutputUsed;
        }

        private unsafe void EncodeBase64(ReadOnlySpan<byte> input, Span<char> output, bool flush, out int inputUsed, out int outputUsed, out bool completed)
        {
            if (input.IsEmpty || output.IsEmpty)
            {
                inputUsed = outputUsed = 0;
                completed = true;
                return;
            }

            // #1: preparing
            const int inputBlockSize = 3;
            const int outputBlockSize = 4;
            const int encodingBits = 6;
            const ulong encodingMask = 63;

            _ = Alphabet.Alphabet ?? throw new InvalidOperationException();

            fixed (char* alphabetPtr = Alphabet.Alphabet)
            fixed (char* outputBytes = &System.Runtime.InteropServices.MemoryMarshal.GetReference(output))
            fixed (byte* inputBytes = &System.Runtime.InteropServices.MemoryMarshal.GetReference(input))
            {
                var inputPtr = inputBytes;
                var outputPtr = outputBytes;

                // #2: encoding whole blocks

                var wholeBlocksToProcess = Math.Min(input.Length / inputBlockSize, output.Length / outputBlockSize);

                inputUsed = inputBlockSize * wholeBlocksToProcess;
                outputUsed = outputBlockSize * wholeBlocksToProcess;

                while (wholeBlocksToProcess-- > 0)
                {
                    // fill input
                    var inputBlock =
                        ((ulong)inputPtr[0] << (8 * 2)) |
                        ((ulong)inputPtr[1] << (8 * 1)) |
                        ((ulong)inputPtr[2] << (8 * 0));

                    // encode input
                    outputPtr[3] = alphabetPtr[(int)((inputBlock >> encodingBits * 0) & encodingMask)];
                    outputPtr[2] = alphabetPtr[(int)((inputBlock >> encodingBits * 1) & encodingMask)];
                    outputPtr[1] = alphabetPtr[(int)((inputBlock >> encodingBits * 2) & encodingMask)];
                    outputPtr[0] = alphabetPtr[(int)((inputBlock >> encodingBits * 3) & encodingMask)];

                    inputPtr += 3;
                    outputPtr += 4;
                }

                input = input.Slice(inputUsed);
                output = output.Slice(outputUsed);
            }

            // #3: encoding any final block (partial with padding)

            EncodeAny(input, output, flush, out var finalInputUsed, out var finalOutputUsed, out completed);

            inputUsed += finalInputUsed;
            outputUsed += finalOutputUsed;
        }
    }
}