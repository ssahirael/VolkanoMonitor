using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace VolcanoMonitor.Services;

public class NeuralNetworkService
{
    private readonly int _inputSize = 5;    // SO2, CO2, H2S, Temp, Seismic
    private readonly int _hiddenSize = 8;
    private readonly int _outputSize = 4;   // Normal, Waspada, Siaga, Awas

    // Weights & Biases
    private double[,] _wInputHidden;
    private double[] _bHidden;
    private double[,] _wHiddenOutput;
    private double[] _bOutput;

    private readonly Random _rand = new(42); // Seed for consistency

    public double InitialLoss { get; private set; }
    public double FinalLoss { get; private set; }
    public double FinalAccuracy { get; private set; }
    public List<(int Epoch, double Loss, double Accuracy)> TrainingHistory { get; } = new();

    public NeuralNetworkService()
    {
        _wInputHidden = new double[_inputSize, _hiddenSize];
        _bHidden = new double[_hiddenSize];
        _wHiddenOutput = new double[_hiddenSize, _outputSize];
        _bOutput = new double[_outputSize];

        InitializeWeights();
        TrainModel();
    }

    private void InitializeWeights()
    {
        // Xavier/Glorot Initialization
        double limitIH = Math.Sqrt(6.0 / (_inputSize + _hiddenSize));
        for (int i = 0; i < _inputSize; i++)
            for (int j = 0; j < _hiddenSize; j++)
                _wInputHidden[i, j] = _rand.NextDouble() * 2 * limitIH - limitIH;

        for (int j = 0; j < _hiddenSize; j++)
            _bHidden[j] = 0.0;

        double limitHO = Math.Sqrt(6.0 / (_hiddenSize + _outputSize));
        for (int j = 0; j < _hiddenSize; j++)
            for (int k = 0; k < _outputSize; k++)
                _wHiddenOutput[j, k] = _rand.NextDouble() * 2 * limitHO - limitHO;

        for (int k = 0; k < _outputSize; k++)
            _bOutput[k] = 0.0;
    }

    private double Sigmoid(double x) => 1.0 / (1.0 + Math.Exp(-x));

    private double[] Softmax(double[] values)
    {
        double max = values.Max();
        double[] exp = values.Select(v => Math.Exp(v - max)).ToArray();
        double sum = exp.Sum();
        return exp.Select(e => e / sum).ToArray();
    }

    private double[] FeedForward(double[] input)
    {
        double[] hidden = new double[_hiddenSize];
        for (int j = 0; j < _hiddenSize; j++)
        {
            double sum = _bHidden[j];
            for (int i = 0; i < _inputSize; i++)
                sum += input[i] * _wInputHidden[i, j];
            hidden[j] = Sigmoid(sum);
        }

        double[] outputRaw = new double[_outputSize];
        for (int k = 0; k < _outputSize; k++)
        {
            double sum = _bOutput[k];
            for (int j = 0; j < _hiddenSize; j++)
                sum += hidden[j] * _wHiddenOutput[j, k];
            outputRaw[k] = sum;
        }
        return Softmax(outputRaw);
    }

    private (double Loss, double Accuracy) ComputeMetrics(List<(double[] Input, int TargetClass)> dataset)
    {
        double totalLoss = 0.0;
        int correct = 0;

        foreach (var sample in dataset)
        {
            double[] output = FeedForward(sample.Input);

            // Cross-entropy: -log(prob kelas benar)
            totalLoss += -Math.Log(Math.Max(output[sample.TargetClass], 1e-12));

            // Argmax = kelas prediksi
            int predicted = 0;
            double maxVal = output[0];
            for (int k = 1; k < _outputSize; k++)
            {
                if (output[k] > maxVal) { maxVal = output[k]; predicted = k; }
            }
            if (predicted == sample.TargetClass) correct++;
        }

        return (totalLoss / dataset.Count, (double)correct / dataset.Count);
    }

    public (string Level, double Confidence) Predict(double so2, double co2, double h2s, double temp, double seismic)
    {
        // 1. Normalize
        double normSO2 = Math.Clamp(so2 / 100.0, 0, 1);
        double normCO2 = Math.Clamp((co2 - 350.0) / 1650.0, 0, 1);
        double normH2S = Math.Clamp(h2s / 20.0, 0, 1);
        double normTemp = Math.Clamp(temp / 1000.0, 0, 1);
        double normSeismic = Math.Clamp(seismic / 10.0, 0, 1);

        double[] input = { normSO2, normCO2, normH2S, normTemp, normSeismic };

        // 2. Feedforward
        double[] output = FeedForward(input);

        // 3. Find Class
        int maxIndex = 0;
        double maxVal = output[0];
        for (int k = 1; k < _outputSize; k++)
        {
            if (output[k] > maxVal)
            {
                maxVal = output[k];
                maxIndex = k;
            }
        }

        string level = maxIndex switch
        {
            0 => "NORMAL",
            1 => "WASPADA",
            2 => "SIAGA",
            _ => "AWAS"
        };

        return (level, maxVal);
    }

    private void TrainModel()
    {
        // Generate training set
        var dataset = new List<(double[] Input, int TargetClass)>();

        // NORMAL: Low sensor readings
        dataset.Add((new[] { 0.01, 0.01, 0.01, 0.05, 0.05 }, 0));
        dataset.Add((new[] { 0.02, 0.03, 0.02, 0.08, 0.10 }, 0));
        dataset.Add((new[] { 0.03, 0.05, 0.03, 0.09, 0.12 }, 0));

        // WASPADA: Mild elevated readings
        dataset.Add((new[] { 0.08, 0.15, 0.10, 0.15, 0.25 }, 1));
        dataset.Add((new[] { 0.12, 0.22, 0.15, 0.20, 0.32 }, 1));
        dataset.Add((new[] { 0.14, 0.25, 0.18, 0.25, 0.38 }, 1));

        // SIAGA: Elevated warnings
        dataset.Add((new[] { 0.25, 0.45, 0.32, 0.35, 0.55 }, 2));
        dataset.Add((new[] { 0.35, 0.58, 0.45, 0.48, 0.68 }, 2));
        dataset.Add((new[] { 0.45, 0.65, 0.52, 0.55, 0.72 }, 2));

        // AWAS: Extreme values
        dataset.Add((new[] { 0.65, 0.82, 0.70, 0.75, 0.88 }, 3));
        dataset.Add((new[] { 0.80, 0.90, 0.85, 0.88, 0.95 }, 3));
        dataset.Add((new[] { 0.95, 0.98, 0.95, 0.95, 0.99 }, 3));

        double lr = 0.15;
        int epochs = 2000;

        var (startLoss, startAcc) = ComputeMetrics(dataset);
        InitialLoss = startLoss;
        TrainingHistory.Add((0, startLoss, startAcc));
        Debug.WriteLine($"[NN] Epoch 0    | loss={startLoss:F4} | acc={startAcc:P1}");

        for (int epoch = 0; epoch < epochs; epoch++)
        {
            foreach (var sample in dataset)
            {
                double[] input = sample.Input;
                int targetClass = sample.TargetClass;

                // One-hot encode target
                double[] target = new double[_outputSize];
                target[targetClass] = 1.0;

                // 1. Feedforward
                double[] hidden = new double[_hiddenSize];
                for (int j = 0; j < _hiddenSize; j++)
                {
                    double sum = _bHidden[j];
                    for (int i = 0; i < _inputSize; i++)
                        sum += input[i] * _wInputHidden[i, j];
                    hidden[j] = Sigmoid(sum);
                }

                double[] outputRaw = new double[_outputSize];
                for (int k = 0; k < _outputSize; k++)
                {
                    double sum = _bOutput[k];
                    for (int j = 0; j < _hiddenSize; j++)
                        sum += hidden[j] * _wHiddenOutput[j, k];
                    outputRaw[k] = sum;
                }
                double[] output = Softmax(outputRaw);

                // 2. Backpropagation
                // Softmax + CrossEntropy gradient: d_k = output_k - target_k
                double[] dOutput = new double[_outputSize];
                for (int k = 0; k < _outputSize; k++)
                {
                    dOutput[k] = output[k] - target[k];
                }

                double[] dHidden = new double[_hiddenSize];
                for (int j = 0; j < _hiddenSize; j++)
                {
                    double errorSum = 0.0;
                    for (int k = 0; k < _outputSize; k++)
                        errorSum += dOutput[k] * _wHiddenOutput[j, k];
                    dHidden[j] = hidden[j] * (1.0 - hidden[j]) * errorSum;
                }

                // 3. Weight Updates (Gradient Descent)
                for (int j = 0; j < _hiddenSize; j++)
                {
                    for (int k = 0; k < _outputSize; k++)
                    {
                        _wHiddenOutput[j, k] -= lr * dOutput[k] * hidden[j];
                    }
                }
                for (int k = 0; k < _outputSize; k++)
                {
                    _bOutput[k] -= lr * dOutput[k];
                }

                for (int i = 0; i < _inputSize; i++)
                {
                    for (int j = 0; j < _hiddenSize; j++)
                    {
                        _wInputHidden[i, j] -= lr * dHidden[j] * input[i];
                    }
                }
                for (int j = 0; j < _hiddenSize; j++)
                {
                    _bHidden[j] -= lr * dHidden[j];
                }
            }

            if ((epoch + 1) % 200 == 0 || epoch == epochs - 1)
            {
                var (loss, acc) = ComputeMetrics(dataset);
                TrainingHistory.Add((epoch + 1, loss, acc));
                Debug.WriteLine($"[NN] Epoch {epoch + 1,-4} | loss={loss:F4} | acc={acc:P1}");
            }
        }

        var (finalLoss, finalAcc) = ComputeMetrics(dataset);
        FinalLoss = finalLoss;
        FinalAccuracy = finalAcc;
        Debug.WriteLine($"[NN] SELESAI | InitialLoss={InitialLoss:F4} | FinalLoss={FinalLoss:F4} | FinalAcc={FinalAccuracy:P1}");
    }
}