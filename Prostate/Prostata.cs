////////////////////////////////////////////////////////////////////////////////
// Prostata.cs
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

            // Buscar PTV de próstata
            var ptv = ss.Structures
                .Where(s => s.Id.ToUpper().Contains("PTV"))
                .OrderByDescending(s => s.Volume)
                .FirstOrDefault();

            if (ptv == null)
                throw new ApplicationException("No se encontró PTV de próstata");

            // Crear plan VMAT
            var plan = course.AddExternalPlanSetup(ss);
            plan.Id = "AUTO_VMAT";

            // Prescripción: 5000 cGy en 25 fracciones (200 cGy/fx)
            plan.SetPrescription(20, new DoseValue(300, DoseValue.DoseUnit.cGy), 1.0);

            // Isocentro en el centro del PTV
            var iso = ptv.CenterPoint;

            // Configurar máquina
            var machine = new ExternalBeamMachineParameters(
                "VitalBeam_4958", "6X", 600, "ARC", "");

            // Campo inicial
            var fieldSize = new VRect<double>(-20, -20, 20, 20);

            // Crear arcos VMAT
            var arc1 = plan.AddArcBeam(machine, fieldSize, 10, 181, 179,
                GantryDirection.Clockwise, 0, iso);

            var arc2 = plan.AddArcBeam(machine, fieldSize, 350, 179, 181,
                GantryDirection.CounterClockwise, 0, iso);

            // Ajustar colimador ANTES de optimizar
            double marginValue = 5; // 5mm para próstata
            var margins = new FitToStructureMargins(marginValue);

            arc1.FitCollimatorToStructure(margins, ptv, true, true, true);
            arc2.FitCollimatorToStructure(margins, ptv, true, true, true);

            // Configurar optimización
            var opt = plan.OptimizationSetup;

            // Buscar órganos de riesgo
            var rectum = ss.Structures.FirstOrDefault(s => s.Id.ToUpper().Contains("RECT"));
            var bladder = ss.Structures.FirstOrDefault(s => s.Id.ToUpper().Contains("BLAD"));
            var femoralHeadL = ss.Structures.FirstOrDefault(s => s.Id.ToUpper().Contains("FEMORALHEAD_L"));
            var femoralHeadR = ss.Structures.FirstOrDefault(s => s.Id.ToUpper().Contains("FEMORALHEAD_R"));
            var penileBulb = ss.Structures.FirstOrDefault(s => s.Id.ToUpper().Contains("PENILE") || s.Id.ToUpper().Contains("BULB"));
            var body = ss.Structures.FirstOrDefault(s => s.Id.ToUpper().Contains("BODY");

            // Objetivos PTV Próstata
            opt.AddPointObjective(ptv,
                OptimizationObjectiveOperator.Lower,
                new DoseValue(6400, DoseValue.DoseUnit.cGy),
                95, 150);  // D95% ≥ 6400 cGy

            opt.AddPointObjective(ptv,
                OptimizationObjectiveOperator.Upper,
                new DoseValue(6400, DoseValue.DoseUnit.cGy),
                100, 90);     // D100% ≤ 6400 cGy

            // Objetivos Recto
            if (rectum != null)
            {
                opt.AddPointObjective(rectum,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(6000, DoseValue.DoseUnit.cGy),
                    01, 80);    // D1% ≤ 6000 cGy

                opt.AddPointObjective(rectum,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(5700, DoseValue.DoseUnit.cGy),
                    10, 60);    // D10% ≤ 5700 cGy

                opt.AddPointObjective(rectum,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(3000, DoseValue.DoseUnit.cGy),
                    35, 50);    // D35% ≤ 3000 cGy
            }

            // Objetivos Vejiga
            if (bladder != null)
            {
                opt.AddPointObjective(bladder,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(6000, DoseValue.DoseUnit.cGy),
                    01, 80);    // D01% ≤ 6000 cGy

                opt.AddPointObjective(bladder,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(5000, DoseValue.DoseUnit.cGy),
                    20, 60);    // D20% ≤ 5000 cGy

                opt.AddPointObjective(bladder,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(3000, DoseValue.DoseUnit.cGy),
                    50, 50);    // D50% ≤ 3000 cGy
            }

            // Objetivos Cabezas Femorales 
            if (femoralHeadL != null)
            {
                opt.AddPointObjective(femoralHeadL,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(3000, DoseValue.DoseUnit.cGy),
                    05, 80);    // D5% ≤ 4000 cGy
            }

            if (femoralHeadR != null)
            {
                opt.AddPointObjective(femoralHeadR,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(3000, DoseValue.DoseUnit.cGy),
                    05, 80);    // D5% ≤ 3000 cGy
            }

            // Objetivos Bulbo Peneano 
            if (penileBulb != null)
            {
                opt.AddPointObjective(penileBulb,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(6000, DoseValue.DoseUnit.cGy),
                    25, 50);    // D25% ≤ 6000 cGy
            }

            //Consideración del body
            if (body != null)
            {
                opt.AddPointObjective(penileBulb,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(6500, DoseValue.DoseUnit.cGy),
                    0, 300);    // D100% ≤ 6500 cGy
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

            catch (Exception e)
            {
                MessageBox.Show("Error en optimización: " + e.Message, "Error");
                return;
            }
        }
    }
}