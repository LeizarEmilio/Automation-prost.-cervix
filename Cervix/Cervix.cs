////////////////////////////////////////////////////////////////////////////////
// Certvix.cs
//
//  A ESAPI v16.1+ script that demonstrates simple plan creation.
//
// Applies to:
//      Eclipse Scripting API
//          16.1
//
// Copyright (c) 2026 Varian Medical Systems, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy 
// of this software and associated documentation files (the "Software"), to deal 
// in the Software without restriction, including without limitation the rights 
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
// copies of the Software, and to permit persons to whom the Software is 
// furnished to do so, subject to the following conditions:
//
//  The above copyright notice and this permission notice shall be included in 
//  all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN 
// THE SOFTWARE.
////////////////////////////////////////////////////////////////////////////////
using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Forms;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

[assembly: ESAPIScript(IsWriteable = true)]

namespace VMS.TPS
{
    public class Script
    {
        public void Execute(ScriptContext context)
        {
            var patient = context.Patient;
            if (patient == null)
                throw new ApplicationException("No hay paciente cargado");

            patient.BeginModifications();

            // Crear o obtener curso AUTO
            var course = patient.Courses.FirstOrDefault(c => c.Id == "AUTO") ?? patient.AddCourse();
            course.Id = "AUTO";

            // Obtener StructureSet
            var ss = patient.StructureSets.FirstOrDefault();
            if (ss == null)
                throw new ApplicationException("No hay StructureSet");

            // Buscar PTV de cérvix
            var ptv = ss.Structures
                .Where(s => s.Id.ToUpper().Contains("PTV"))
                .OrderByDescending(s => s.Volume)
                .FirstOrDefault();

            if (ptv == null)
                throw new ApplicationException("No se encontró PTV de cérvix");

            // Crear plan VMAT
            var plan = course.AddExternalPlanSetup(ss);
            plan.Id = "AUTO_VMAT";

            // Prescripción: 5000 cGy en 25 fracciones (200 cGy/fx)
            plan.SetPrescription(25, new DoseValue(200, DoseValue.DoseUnit.cGy), 1.0);

            // Isocentro en el centro del PTV
            var iso = ptv.CenterPoint;

            // Configurar máquina
            var machine = new ExternalBeamMachineParameters(
                "VitalBeam_4958", "6X", 600, "ARC", "");

            // Campo inicial (más grande para pelvis)
            var fieldSize = new VRect<double>(-7.5, -20, 7.5, 20);

            // Crear arcos VMAT (generalmente 2-3 arcos para cérvix)
            var arc1 = plan.AddArcBeam(machine, fieldSize, 10, 181, 179,
                GantryDirection.Clockwise, 0, iso);

            var arc2 = plan.AddArcBeam(machine, fieldSize, 350, 179, 181,
                GantryDirection.CounterClockwise, 0, iso);



            // Ajustar colimador ANTES de optimizar
            double marginValue = 10; // 10mm para cérvix (más margen)
            var margins = new FitToStructureMargins(marginValue);

            arc1.FitCollimatorToStructure(margins, ptv, true, true, true);
            arc2.FitCollimatorToStructure(margins, ptv, true, true, true);

            // Configurar optimización
            var opt = plan.OptimizationSetup;

            // Buscar órganos de riesgo para cérvix
            var rectum = ss.Structures.FirstOrDefault(s => s.Id.ToUpper().Contains("RECT"));
            var bladder = ss.Structures.FirstOrDefault(s => s.Id.ToUpper().Contains("BLAD"));
            var bowel = ss.Structures.FirstOrDefault(s => s.Id.ToUpper().Contains("BOWEL") || s.Id.ToUpper().Contains("BOLSA"));
            var femoralHeadL = ss.Structures.FirstOrDefault(s => s.Id.ToUpper().Contains("FEMORALHEAD_L"));
            var femoralHeadR = ss.Structures.FirstOrDefault(s => s.Id.ToUpper().Contains("FEMORALHEAD_R"));
            var medula = ss.Structures.FirstOrDefault(s => s.Id.ToUpper().Contains("SPINAL"));
            var kidneyL = ss.Structures.FirstOrDefault(s => s.Id.ToUpper().Contains("KIDNEY_L"));
            var kidneyR = ss.Structures.FirstOrDefault(s => s.Id.ToUpper().Contains("KIDNEY_R"));
            var body = ss.Structures.FirstOrDefault(s => s.Id.ToUpper().Contains("BODY"));
            var sigmo = ss.Structures.FirstOrDefault(s => s.Id.ToUpper().Contains("SIGM"));

            // Objetivos PTV Cérvix
            opt.AddPointObjective(ptv,
                OptimizationObjectiveOperator.Lower,
                new DoseValue(5100, DoseValue.DoseUnit.cGy),
                100, 80);  // D100% ≥ 5100 cGy

            opt.AddPointObjective(ptv,
                OptimizationObjectiveOperator.Upper,
                new DoseValue(5400, DoseValue.DoseUnit.cGy),
                0, 80);     // D0% ≤ 5400 cGy

            opt.AddPointObjective(ptv,
                OptimizationObjectiveOperator.Lower,
                new DoseValue(5200, DoseValue.DoseUnit.cGy),
                95, 150);   // V95% ≥ 5200 cGy

            // Objetivos Recto
            if (rectum != null)
            {
                opt.AddPointObjective(rectum,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(3500, DoseValue.DoseUnit.cGy),
                    75, 60);    // D75% ≤ 3500 cGy

                opt.AddPointObjective(rectum,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(2000, DoseValue.DoseUnit.cGy),
                    95, 70);    // D95% ≤ 2000 cGy

                opt.AddPointObjective(rectum,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(4000, DoseValue.DoseUnit.cGy),
                    50, 80);    // D50% ≤ 4000 cGy

                opt.AddPointObjective(rectum,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(5000, DoseValue.DoseUnit.cGy),
                    01, 80);    // D01% ≤ 5000 cGy
            }

            // Objetivos Vejiga
            if (bladder != null)
            {
                opt.AddPointObjective(bladder,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(2800, DoseValue.DoseUnit.cGy),
                    80, 60);    // D80% ≤ 2800 cGy

                opt.AddPointObjective(bladder,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(4000, DoseValue.DoseUnit.cGy),
                    50, 60);    // D50% ≤ 4000 cGy

                opt.AddPointObjective(bladder,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(3500, DoseValue.DoseUnit.cGy),
                    40, 50);    // D40% ≤ 3500 cGy

                opt.AddPointObjective(bladder,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(3800, DoseValue.DoseUnit.cGy),
                    35, 70);    // D35% ≤ 3800 cGy
            }

            // Objetivos Intestino
            if (bowel != null)
            {
                opt.AddPointObjective(bowel,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(4000, DoseValue.DoseUnit.cGy),
                    01, 70);    // D30% ≤ 4500 cGy

                opt.AddPointObjective(bowel,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(4800, DoseValue.DoseUnit.cGy),
                    0, 50);    // Dmax% ≤ 4800 cGy
            }

            // Objetivos Cabezas Femorales
            if (femoralHeadL != null)
            {
                opt.AddPointObjective(femoralHeadL,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(3000, DoseValue.DoseUnit.cGy),
                    05, 80);    // D5% ≤ 3000 cGy

                opt.AddPointObjective(femoralHeadR,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(4000, DoseValue.DoseUnit.cGy),
                    0, 80);    // D5% ≤ 3000 cGy
            }

            if (femoralHeadR != null)
            {
                opt.AddPointObjective(femoralHeadR,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(3000, DoseValue.DoseUnit.cGy),
                    05, 80);    // D5% ≤ 3000 cGy

                opt.AddPointObjective(femoralHeadR,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(4000, DoseValue.DoseUnit.cGy),
                    0, 80);    // D5% ≤ 3000 cGy
            }

            // Objetivos Médula Espinal
            if (medula != null)
            {
                opt.AddPointObjective(medula,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(3800, DoseValue.DoseUnit.cGy),
                    0, 90);     // Dmax% ≤ 3800 cGy
            }

            // Objetivos Riñones 
            if (kidneyL != null)
            {
                opt.AddMeanDoseObjective(kidneyL,
                    new DoseValue(1400, DoseValue.DoseUnit.cGy),
                    100);    // D50% ≤ 1400 cGy

                opt.AddPointObjective(kidneyL,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(1200, DoseValue.DoseUnit.cGy),
                    30, 80);    // D30% ≤ 1200 cGy
            }

            if (kidneyR != null)
            {
                opt.AddMeanDoseObjective(kidneyR,
                    new DoseValue(1400, DoseValue.DoseUnit.cGy),
                    100);    // D50% ≤ 1400 cGy

                opt.AddPointObjective(kidneyR,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(1200, DoseValue.DoseUnit.cGy),
                    30, 80);  // D30% ≤ 1200 cGy
            }

            // Objetivo sigmoides
            if (sigmo != null)
            {
                opt.AddPointObjective(sigmo,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(4500, DoseValue.DoseUnit.cGy),
                    1, 90);
            }

            //Concideracion del body
            if (body != null)
            {
                opt.AddPointObjective(body,
                     OptimizationObjectiveOperator.Upper,
                     new DoseValue(5400, DoseValue.DoseUnit.cGy),
                     0, 300);    // Dmax% ≤ 5400 cGy
            }

            // Configurar algoritmos de cálculo
            plan.SetCalculationModel(CalculationType.PhotonVolumeDose, "CAP_AAA_1610");
            plan.SetCalculationModel(CalculationType.PhotonOptimization, "CAP_PO_1610");

            try
            {
                plan.OptimizeVMAT();
                plan.CalculateDose();

                // Normalizar
                plan.PlanNormalizationValue = 100;

                // Calcular D95 y otros índices
                var dvh = plan.GetDVHCumulativeData(
                    ptv,
                    DoseValuePresentation.Absolute,
                    VolumePresentation.Relative,
                    0.1);

                double d95 = dvh.CurveData.FirstOrDefault(p => p.Volume <= 95).DoseValue.Dose;
                double d98 = dvh.CurveData.FirstOrDefault(p => p.Volume <= 98).DoseValue.Dose;
                double d2 = dvh.CurveData.FirstOrDefault(p => p.Volume <= 2).DoseValue.Dose;
            }
            catch (Exception e)
            {
                MessageBox.Show("Error en optimización: " + e.Message, "Error");
                return;
            }
        }
    }
}