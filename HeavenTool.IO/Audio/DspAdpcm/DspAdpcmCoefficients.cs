using static HeavenTool.IO.Audio.DspAdpcm.DspAdpcmCodec;

namespace HeavenTool.IO.Audio.DspAdpcm;

public static class DspAdpcmCoefficients
{
    private readonly record struct PredictorRecord(double Coefficient1, double Coefficient2);

    public static short[] CalculateCoefficients(ReadOnlySpan<short> samples)
    {
        int frameCount = samples.Length / SamplesPerFrame + (samples.Length % SamplesPerFrame == 0 ? 0 : 1);
        var records = new PredictorRecord[frameCount];
        int recordCount = 0;
        Span<short> history = stackalloc short[SamplesPerFrame * 2];
        history.Clear();
        var correlation = new double[3];
        var covariance = CreateVectors(3);
        var pivots = new int[3];
        var rowScales = new double[3];

        for (int position = 0; position < samples.Length;)
        {
            int count = Math.Min(SamplesPerFrame, samples.Length - position);
            history[SamplesPerFrame..].Clear();
            samples.Slice(position, count).CopyTo(history[SamplesPerFrame..]);
            CalculateAutocorrelation(correlation, history);
            if (Math.Abs(correlation[0]) > 10.0)
            {
                CalculateCovariance(covariance, history);
                if (TryFactorizeMatrix(covariance, pivots, rowScales))
                {
                    SolvePredictor(covariance, pivots, correlation);
                    if (TryNormalizePredictor(correlation))
                        records[recordCount++] = CreateRecord(correlation);
                }
            }
            history[SamplesPerFrame..].CopyTo(history);
            position += count;
        }

        var coefficients = new short[CoefficientCount];
        if (recordCount == 0) 
            return coefficients;

        var predictors = CreateVectors(PredictorCount);
        correlation[0] = 1.0;
        correlation[1] = correlation[2] = 0.0;

        foreach (var record in records.AsSpan(0, recordCount))
        {
            FilterRecord(record, predictors[0], covariance);
            for (int i = 1; i <= 2; i++) correlation[i] += predictors[0][i];
        }

        for (int i = 1; i <= 2; i++) 
            correlation[i] /= recordCount;

        BuildPredictor(correlation, predictors[0]);

        ReadOnlySpan<double> perturbation = [0.0, -1.0, 0.0];
        for (int count = 1; count < PredictorCount; count *= 2)
        {
            for (int i = 0; i < count; i++)
                for (int component = 0; component <= 2; component++)
                    predictors[count + i][component] = (0.01 * perturbation[component]) + predictors[i][component];
            RefinePredictors(predictors, count * 2, records.AsSpan(0, recordCount));
        }

        for (int i = 0; i < PredictorCount; i++)
        {
            coefficients[i * 2] = QuantizeCoefficient(-predictors[i][1] * CoefficientScale);
            coefficients[i * 2 + 1] = QuantizeCoefficient(-predictors[i][2] * CoefficientScale);
        }

        return coefficients;
    }

    private static short QuantizeCoefficient(double value) => (short)Math.Clamp(Math.Round(value), short.MinValue, short.MaxValue);

    private static double[][] CreateVectors(int count)
    {
        var vectors = new double[count][];
        for (int i = 0; i < count; i++) 
            vectors[i] = new double[3];
        return vectors;
    }

    private static void CalculateAutocorrelation(double[] vector, ReadOnlySpan<short> history)
    {
        for (int i = 0; i <= 2; i++)
        {
            vector[i] = 0.0;
            for (int x = 0; x < SamplesPerFrame; x++)
                vector[i] -= history[SamplesPerFrame + x - i] * history[SamplesPerFrame + x];
        }
    }

    private static void CalculateCovariance(double[][] matrix, ReadOnlySpan<short> history)
    {
        for (int x = 1; x <= 2; x++)
            for (int y = 1; y <= 2; y++)
            {
                matrix[x][y] = 0.0;
                for (int z = 0; z < SamplesPerFrame; z++)
                    matrix[x][y] += history[SamplesPerFrame + z - x] * history[SamplesPerFrame + z - y];
            }
    }

    private static bool TryFactorizeMatrix(double[][] matrix, int[] pivots, double[] rowScales)
    {
        double value, temporary, min, max;

        // Scale each row before choosing pivots.
        for (int x = 1; x <= 2; x++)
        {
            value = Math.Max(Math.Abs(matrix[x][1]), Math.Abs(matrix[x][2]));
            if (value < double.Epsilon)
                return false;

            rowScales[x] = 1.0 / value;
        }

        int maxIndex = 0;
        for (int i = 1; i <= 2; i++)
        {
            for (int x = 1; x < i; x++)
            {
                temporary = matrix[x][i];
                for (int y = 1; y < x; y++)
                    temporary -= matrix[x][y] * matrix[y][i];
                matrix[x][i] = temporary;
            }

            value = 0.0;
            for (int x = i; x <= 2; x++)
            {
                temporary = matrix[x][i];
                for (int y = 1; y < i; y++)
                    temporary -= matrix[x][y] * matrix[y][i];

                matrix[x][i] = temporary;
                temporary = Math.Abs(temporary) * rowScales[x];
                if (temporary >= value)
                {
                    value = temporary;
                    maxIndex = x;
                }
            }

            if (maxIndex != i)
            {
                for (int y = 1; y <= 2; y++)
                {
                    temporary = matrix[maxIndex][y];
                    matrix[maxIndex][y] = matrix[i][y];
                    matrix[i][y] = temporary;
                }
                rowScales[maxIndex] = rowScales[i];
            }

            pivots[i] = maxIndex;

            if (i != 2)
            {
                temporary = 1.0 / matrix[i][i];
                for (int x = i + 1; x <= 2; x++)
                    matrix[x][i] *= temporary;
            }
        }

        // Reject ill-conditioned predictors.
        min = 1.0e10;
        max = 0.0;
        for (int i = 1; i <= 2; i++)
        {
            temporary = Math.Abs(matrix[i][i]);
            if (temporary < min)
                min = temporary;
            if (temporary > max)
                max = temporary;
        }

        return !(min / max < 1.0e-10);
    }

    private static void SolvePredictor(double[][] matrix, int[] pivots, double[] vector)
    {
        double temporary;

        for (int i = 1, x = 0; i <= 2; i++)
        {
            int index = pivots[i];
            temporary = vector[index];
            vector[index] = vector[i];
            if (x != 0)
                for (int y = x; y <= i - 1; y++)
                    temporary -= vector[y] * matrix[i][y];
            else if (temporary != 0.0)
                x = i;
            vector[i] = temporary;
        }

        for (int i = 2; i > 0; i--)
        {
            temporary = vector[i];
            for (int y = i + 1; y <= 2; y++)
                temporary -= vector[y] * matrix[i][y];
            vector[i] = temporary / matrix[i][i];
        }

        vector[0] = 1.0;
    }

    private static bool TryNormalizePredictor(double[] predictor)
    {
        double v2 = predictor[2];
        double temporary = 1.0 - (v2 * v2);
        if (temporary == 0.0)
            return false;

        double v0 = (predictor[0] - (v2 * v2)) / temporary;
        double v1 = (predictor[1] - (predictor[1] * v2)) / temporary;

        predictor[0] = v0;
        predictor[1] = v1;

        return !(Math.Abs(v1) > 1.0);
    }

    private static PredictorRecord CreateRecord(Span<double> reflection)
    {
        for (int z = 1; z <= 2; z++)
        {
            if (reflection[z] >= 1.0)
                reflection[z] = 0.9999999999;
            else if (reflection[z] <= -1.0)
                reflection[z] = -0.9999999999;
        }
        return new PredictorRecord((reflection[2] * reflection[1]) + reflection[1], reflection[2]);
    }

    private static void FilterRecord(PredictorRecord record, double[] destination, double[][] matrix)
    {
        matrix[2][0] = 1.0;
        matrix[2][1] = -record.Coefficient1;
        matrix[2][2] = -record.Coefficient2;

        for (int i = 2; i > 0; i--)
        {
            double value = 1.0 - (matrix[i][i] * matrix[i][i]);
            for (int y = 1; y <= i; y++)
                matrix[i - 1][y] = ((matrix[i][i] * matrix[i][y]) + matrix[i][y]) / value;
        }

        destination[0] = 1.0;
        for (int i = 1; i <= 2; i++)
        {
            destination[i] = 0.0;
            for (int y = 1; y <= i; y++)
                destination[i] += matrix[i][y] * destination[i - y];
        }
    }

    private static void BuildPredictor(double[] source, double[] destination)
    {
        Span<double> temporary = stackalloc double[3];
        double value = source[0];

        destination[0] = 1.0;
        for (int i = 1; i <= 2; i++)
        {
            double v2 = 0.0;
            for (int y = 1; y < i; y++)
                v2 += destination[y] * source[i - y];

            if (value > 0.0)
                destination[i] = -(v2 + source[i]) / value;
            else
                destination[i] = 0.0;

            temporary[i] = destination[i];

            for (int y = 1; y < i; y++)
                destination[y] += destination[i] * destination[i - y];

            value *= 1.0 - (destination[i] * destination[i]);
        }

        var record = CreateRecord(temporary);
        destination[0] = 1.0;
        destination[1] = record.Coefficient1;
        destination[2] = record.Coefficient2;
    }

    private static double PredictorDistance(double[] predictor, PredictorRecord record)
    {
        double value = (record.Coefficient2 * record.Coefficient1 + -record.Coefficient1) / (1.0 - record.Coefficient2 * record.Coefficient2);
        double energy = (predictor[0] * predictor[0]) + (predictor[1] * predictor[1]) + (predictor[2] * predictor[2]);
        double adjacentProduct = (predictor[0] * predictor[1]) + (predictor[1] * predictor[2]);
        double outerProduct = predictor[0] * predictor[2];
        return energy + (2.0 * value * adjacentProduct) + (2.0 * (-record.Coefficient1 * value + -record.Coefficient2) * outerProduct);
    }

    private static void RefinePredictors(double[][] predictors, int predictorCount, ReadOnlySpan<PredictorRecord> records)
    {
        var sums = CreateVectors(PredictorCount);
        var matrix = CreateVectors(3);
        var counts = new int[PredictorCount];
        var filtered = new double[3];

        for (int iteration = 0; iteration < 2; iteration++)
        {
            for (int i = 0; i < predictorCount; i++)
            {
                counts[i] = 0;
                Array.Clear(sums[i]);
            }
            foreach (var record in records)
            {
                int best = 0;
                double bestDistance = 1.0e30;
                for (int i = 0; i < predictorCount; i++)
                {
                    double distance = PredictorDistance(predictors[i], record);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = i;
                    }
                }
                counts[best]++;
                FilterRecord(record, filtered, matrix);
                for (int i = 0; i <= 2; i++) sums[best][i] += filtered[i];
            }
            for (int i = 0; i < predictorCount; i++)
            {
                if (counts[i] > 0)
                    for (int component = 0; component <= 2; component++)
                        sums[i][component] /= counts[i];
                BuildPredictor(sums[i], predictors[i]);
            }
        }
    }
}