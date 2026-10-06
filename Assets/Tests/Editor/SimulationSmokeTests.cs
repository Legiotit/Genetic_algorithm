using NUnit.Framework;
using UnityEngine;
using GpuSim;

namespace GpuSim.Tests
{
    // Дымные тесты всего конвейера: первичный спавн, несколько шагов с реальной
    // случайной популяцией, поддержка популяции спавном и здравость статистики.
    // Не утверждают конкретных чисел (ДНК случайна) — проверяют, что pipeline
    // не падает и метрики в разумных пределах.
    public class SimulationSmokeTests
    {
        [Test]
        public void PrimarySpawn_PopulatesField()
        {
            var go = new GameObject("smoke");
            var sim = go.AddComponent<GpuSimulation>();
            try
            {
                sim.Configure(32, 32, 12, 20, 30, 0.10f, false);
                sim.TestLightMode = 1;
                sim.TestLightLevel = 1f;
                sim.TestTargetPopulation = 100;
                sim.TestSpawnEnabled = false;
                sim.TestDraw = false;
                sim.StartSim();
                sim.TestStep(); // прогон, чтобы статистика собралась

                int[] stats = sim.TestReadStats();
                Assert.Greater(stats[0], 0, "После первичного спавна есть живые клетки");
                Assert.LessOrEqual(stats[0], 100, "Живых не больше, чем заспавнено");
            }
            finally { TestUtil.Cleanup(sim); }
        }

        [Test]
        public void RunManySteps_StatsStaySane()
        {
            var go = new GameObject("smoke");
            var sim = go.AddComponent<GpuSimulation>();
            try
            {
                sim.Configure(32, 32, 12, 20, 30, 0.10f, false);
                sim.TestLightMode = 1;
                sim.TestLightLevel = 1f;
                sim.TestTargetPopulation = 100;
                sim.TestSpawnEnabled = true;   // поддержка популяции
                sim.TestDraw = false;
                sim.StartSim();

                for (int i = 0; i < 30; i++) sim.TestStep();

                int[] stats = sim.TestReadStats();
                // _Step в шейдере выставляется до инкремента C#-счётчика,
                // поэтому stats[2] (0-индекс последнего шага) = 29, а sim.Step = 30.
                Assert.AreEqual(30, sim.Step, "C#-счётчик шагов = 30");
                Assert.AreEqual(29, stats[2], "В статистике — индекс последнего выполненного шага");
                Assert.GreaterOrEqual(stats[0], 0, "alive ≥ 0");
                Assert.GreaterOrEqual(stats[3], 0, "totalEnergy ≥ 0");
                // Со свободным светом и спавном популяция не должна вымереть за 30 шагов.
                Assert.Greater(stats[0], 0, "Популяция выживает при наличии света и спавна");
            }
            finally { TestUtil.Cleanup(sim); }
        }

        [Test]
        public void Step_IncrementsStepCounter()
        {
            var sim = TestUtil.MakeSim(maxCmd: 1, initE: 50);
            try
            {
                sim.TestSetupCells(new GpuSimulation.TestCellSpec
                {
                    x = 8, y = 8, dna = TestUtil.DnaRepeat(TestUtil.OP_GOTO, 0), genes = 0, energy = 50
                });

                Assert.AreEqual(0, sim.Step);
                sim.TestStep();
                Assert.AreEqual(1, sim.Step);
                sim.TestStep();
                Assert.AreEqual(2, sim.Step);
            }
            finally { TestUtil.Cleanup(sim); }
        }
    }
}