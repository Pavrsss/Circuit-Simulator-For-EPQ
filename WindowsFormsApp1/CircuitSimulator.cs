using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace WindowsFormsApp1
{
    public static class CircuitSimulator
    {


        public static double GetEffectiveResistance(Form1.BaseElecPiece comp, double appliedVoltage = 0.0)
        {
            if (comp == null) return double.PositiveInfinity;

            Type t = comp.GetType();

            MethodInfo mi = t.GetMethod("EffectiveResistanceForVoltage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi != null)
            {
                try
                {
                    var result = mi.Invoke(comp, new object[] { appliedVoltage });
                    if (result is double d) return d;
                    if (result != null && double.TryParse(result.ToString(), out d)) return d;
                }
                catch { }
            }

            PropertyInfo pf = t.GetProperty("ForwardResistance", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            PropertyInfo pr = t.GetProperty("ReverseResistance", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (pf != null && pr != null)
            {
                try
                {
                    double f = Convert.ToDouble(pf.GetValue(comp));
                    double r = Convert.ToDouble(pr.GetValue(comp));
                    return appliedVoltage >= 0 ? f : r;
                }
                catch { }
            }

            if (string.Equals(t.Name, "Resistor", StringComparison.OrdinalIgnoreCase))
            {
                return Math.Max(0.0, Math.Min(1000.0, comp.Resistance));
            }

            return comp.Resistance;
        }

        public static double ComputeCircuitCurrent(IEnumerable<Form1.BaseElecPiece> components, double additionalOscillatorVoltage = 0.0, bool isParallel = false)
        {
            if (components == null)
            {
                return 0.0;
            }

            var activeComponents = components
                .Where(component =>
                    component != null &&
                    !(component is Form1.Wire) &&
                    !(component is Form1.Ammeter) &&
                    !(component is Form1.Voltmeter))
                .ToList();

            if (activeComponents.Count == 0)
            {
                return 0.0;
            }

            double sourceVoltage = additionalOscillatorVoltage +
                activeComponents
                    .Where(component =>
                        component is Form1.Battery ||
                        component is Form1.Cell)
                    .Sum(component => component.Voltage);

            var loadComponents = activeComponents
                .Where(component =>
                    !(component is Form1.Battery) &&
                    !(component is Form1.Cell))
                .ToList();

            if (loadComponents.Count == 0)
            {
                return 0.0;
            }

            if (!isParallel)
            {
                double totalResistance = loadComponents
                    .Sum(component => GetEffectiveResistance(component, sourceVoltage));

                if (totalResistance <= 0.0 || double.IsPositiveInfinity(totalResistance))
                {
                    return 0.0;
                }

                return sourceVoltage / totalResistance;
            }

            double totalConductance = 0.0;

            foreach (var component in loadComponents)
            {
                double resistance = GetEffectiveResistance(component, sourceVoltage);

                if (resistance > 0.0 && !double.IsPositiveInfinity(resistance))
                {
                    totalConductance += 1.0 / resistance;
                }
            }

            if (totalConductance <= 0.0)
            {
                return 0.0;
            }

            double equivalentResistance = 1.0 / totalConductance;
            return sourceVoltage / equivalentResistance;
        }

        public static double ComputeBranchCurrent(
            IEnumerable<Form1.BaseElecPiece> branchComponents,
            double sourceVoltage)
        {
            if (branchComponents == null)
            {
                return 0.0;
            }

            double branchResistance = branchComponents
                .Where(component =>
                    component != null &&
                    !(component is Form1.Wire) &&
                    !(component is Form1.Ammeter) &&
                    !(component is Form1.Voltmeter) &&
                    !(component is Form1.Battery) &&
                    !(component is Form1.Cell))
                .Sum(component => GetEffectiveResistance(component, sourceVoltage));

            if (branchResistance <= 0.0 ||
                double.IsPositiveInfinity(branchResistance))
            {
                return 0.0;
            }

            return sourceVoltage / branchResistance;
        }

        public static double[] ComputeParallelBranchCurrents(
            IEnumerable<IEnumerable<Form1.BaseElecPiece>> parallelBranches,
            double sourceVoltage)
        {
            if (parallelBranches == null)
            {
                return Array.Empty<double>();
            }

            return parallelBranches
                .Select(branch => ComputeBranchCurrent(branch, sourceVoltage))
                .ToArray();
        }

        public static double ComputeTotalCurrent(IEnumerable<double> branchCurrents)
        {
            return branchCurrents?.Sum() ?? 0.0;
        }
    }
}