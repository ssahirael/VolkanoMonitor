using System;

namespace VolcanoMonitor.Services;

public class FuzzyLogicService
{
    // Membership function helpers
    private double GetMembershipLow(double val, double minMid, double maxLow)
    {
        if (val <= minMid) return 1.0;
        if (val >= maxLow) return 0.0;
        return (maxLow - val) / (maxLow - minMid);
    }

    private double GetMembershipMedium(double val, double start, double peak, double end)
    {
        if (val <= start || val >= end) return 0.0;
        if (Math.Abs(val - peak) < 1e-9) return 1.0;
        if (val < peak) return (val - start) / (peak - start);
        return (end - val) / (end - peak);
    }

    private double GetMembershipHigh(double val, double startHigh, double peakHigh)
    {
        if (val >= peakHigh) return 1.0;
        if (val <= startHigh) return 0.0;
        return (val - startHigh) / (peakHigh - startHigh);
    }

    // Risk Output Membership Functions for Centroid Defuzzification
    private double RiskLowMF(double r) => GetMembershipLow(r, 15, 40);
    private double RiskMediumMF(double r) => GetMembershipMedium(r, 25, 50, 75);
    private double RiskHighMF(double r) => GetMembershipHigh(r, 60, 85);

    public double EvaluateRisk(double so2, double co2, double h2s, double temp, double seismic)
    {
        // 1. Fuzzification
        // SO2 ranges (ppm)
        double so2Low = GetMembershipLow(so2, 3, 10);
        double so2Med = GetMembershipMedium(so2, 5, 15, 25);
        double so2High = GetMembershipHigh(so2, 20, 35);

        // CO2 ranges (ppm)
        double co2Low = GetMembershipLow(co2, 400, 700);
        double co2Med = GetMembershipMedium(co2, 600, 1000, 1400);
        double co2High = GetMembershipHigh(co2, 1200, 1800);

        // H2S ranges (ppm)
        double h2sLow = GetMembershipLow(h2s, 0.5, 3.0);
        double h2sMed = GetMembershipMedium(h2s, 2.0, 6.0, 10.0);
        double h2sHigh = GetMembershipHigh(h2s, 8.0, 15.0);

        // Temp ranges (°C)
        double tempLow = GetMembershipLow(temp, 70, 160);
        double tempMed = GetMembershipMedium(temp, 120, 280, 420);
        double tempHigh = GetMembershipHigh(temp, 350, 600);

        // Seismic ranges (index 0 - 10)
        double seismicLow = GetMembershipLow(seismic, 1.5, 4.0);
        double seismicMed = GetMembershipMedium(seismic, 3.0, 6.0, 8.5);
        double seismicHigh = GetMembershipHigh(seismic, 7.5, 9.5);

        // 2. Evaluate Rules (Mamdani T-Norm: AND = min, T-Conorm: OR = max)

        // Rule 1: IF SO2 is High OR Temp is High THEN Risk is High
        double r1 = Math.Max(so2High, tempHigh);

        // Rule 2: IF Seismic is High AND Temp is High THEN Risk is High
        double r2 = Math.Min(seismicHigh, tempHigh);

        // Rule 3: IF SO2 is Medium AND Temp is Medium THEN Risk is Medium
        double r3 = Math.Min(so2Med, tempMed);

        // Rule 4: IF CO2 is Medium OR H2S is Medium THEN Risk is Medium
        double r4 = Math.Max(co2Med, h2sMed);

        // Rule 5: IF SO2 is Low AND CO2 is Low AND Temp is Low AND Seismic is Low THEN Risk is Low
        double r5 = Math.Min(Math.Min(so2Low, co2Low), Math.Min(tempLow, seismicLow));

        // Aggregate Activations
        double actHigh = Math.Max(r1, r2);
        double actMed = Math.Max(r3, r4);
        double actLow = r5;

        // 3. Defuzzification using Centroid/Center-of-Area Method
        double sumProduct = 0.0;
        double sumMembership = 0.0;

        // Sample risk index from 0% to 100% in steps of 2%
        for (double r = 0; r <= 100; r += 2)
        {
            // Sample membership at this point using Mamdani clipping (min with activation)
            double mfLow = Math.Min(RiskLowMF(r), actLow);
            double mfMed = Math.Min(RiskMediumMF(r), actMed);
            double mfHigh = Math.Min(RiskHighMF(r), actHigh);

            // Max aggregation
            double aggregatedMF = Math.Max(Math.Max(mfLow, mfMed), mfHigh);

            sumProduct += r * aggregatedMF;
            sumMembership += aggregatedMF;
        }

        // If no rules fired (all memberships are zero), default to 0% risk
        if (sumMembership < 1e-6)
        {
            return 0.0;
        }

        return sumProduct / sumMembership;
    }

    public string GetExplanation(double riskIndex, string nnLevel)
    {
        string riskLabel = riskIndex switch
        {
            < 30 => "RENDAH",
            < 65 => "SEDANG",
            < 85 => "TINGGI",
            _ => "SANGAT TINGGI/KRITIS"
        };

        string actionText = nnLevel switch
        {
            "NORMAL" => "Kondisi stabil. Aman bagi penduduk setempat untuk melakukan aktivitas normal harian di sekitar lereng.",
            "WASPADA" => "Terjadi peningkatan aktivitas visual/seismik ringan. Warga diimbau memantau info PVMBG dan menghindari mulut kawah dalam radius 1.5 km.",
            "SIAGA" => "Kubah lava atau emisi gas meningkat tajam. Harap mengosongkan kawasan rawan bencana II dan mengungsi dalam radius 3 - 5 km.",
            _ => "Ancaman letusan besar sangat dekat. Evakuasi wajib segera dilakukan bagi seluruh penduduk dalam radius bahaya 5 - 10 km!"
        };

        return $"Sistem kecerdasan buatan menyimpulkan tingkat ancaman [{riskLabel}] dengan indeks risiko {riskIndex:F1}% (Neural Network level: {nnLevel}).\n\nRekomendasi mitigasi: {actionText}";
    }
}