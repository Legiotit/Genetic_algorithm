using NUnit.Framework;
using GpuSim;

namespace GpuSim.Tests
{
    // Проверка фотосинтеза: клетка с ДНК из одних photosynthesis-команд
    // должна накапливать энергию согласно light*lightLevel*5 за команду.
    public class PhotosynthesisTests
    {
        // ДНК: бесконечный цикл photosynthesis (op=40, смещение 0 → ip += 2).
        static uint[] PhotoDna() => TestUtil.DnaRepeat(TestUtil.OP_PHOTO, 0);

        [Test]
        public void Photosynthesis_GainsEnergy_ProportionalToCommandCount()
        {
            // maxCommand=12 → 12 команд фотосинтеза за шаг.
            // Каждая: +ceil(1*1*5)=5, -cost(1, em=1)=1 → +4. Базовый расход −1.
            // 20 + 12*4 − 1 = 67.
            var sim = TestUtil.MakeSim(maxCmd: 12, initE: 20, lightMode: 1, lightLevel: 1f);
            try
            {
                sim.TestSetupCells(new GpuSimulation.TestCellSpec
                {
                    x = 8, y = 8, dir = 0, ip = 0,
                    dna = PhotoDna(), genes = 0, energy = 20
                });

                sim.TestStep();
                int e = sim.TestReadEnergy()[0];
                Assert.AreEqual(67, e, "После 1 шага 12 фотосинтезов энергия должна быть 67");
            }
            finally { TestUtil.Cleanup(sim); }
        }

        [Test]
        public void Photosynthesis_RespectsLightLevelMultiplier()
        {
            // lightLevel=0.5 → +ceil(1*0.5*5)=ceil(2.5)=3, −1 → +2 за команду.
            // 20 + 12*2 − 1 = 43.
            var sim = TestUtil.MakeSim(maxCmd: 12, initE: 20, lightMode: 1, lightLevel: 0.5f);
            try
            {
                sim.TestSetupCells(new GpuSimulation.TestCellSpec
                {
                    x = 8, y = 8, dna = PhotoDna(), genes = 0, energy = 20
                });

                sim.TestStep();
                int e = sim.TestReadEnergy()[0];
                Assert.AreEqual(43, e, "При lightLevel=0.5 энергия за шаг должна быть 43");
            }
            finally { TestUtil.Cleanup(sim); }
        }

        [Test]
        public void Photosynthesis_EnergyCapsAt255()
        {
            var sim = TestUtil.MakeSim(maxCmd: 12, initE: 20, lightMode: 1, lightLevel: 2f);
            try
            {
                sim.TestSetupCells(new GpuSimulation.TestCellSpec
                {
                    x = 8, y = 8, dna = PhotoDna(), genes = 0, energy = 20
                });

                // lightLevel=2 → +10/команду, кап 255 достигается быстро.
                for (int i = 0; i < 8; i++) sim.TestStep();
                int e = sim.TestReadEnergy()[0];
                Assert.AreEqual(255, e, "Энергия не может превышать 255");
                // клетка остаётся живой
                Assert.AreEqual(SimFlags.ALIVE, sim.TestReadCells()[0].flags & SimFlags.ALIVE);
            }
            finally { TestUtil.Cleanup(sim); }
        }

        [Test]
        public void Photosynthesis_WithZeroLight_StavesAndDies()
        {
            // lightLevel=0 → +0 за команду, −1 расход + базовый −1.
            // Шаг 1: 20 − 13 = 7 (жива). Шаг 2: 7 → 0 (break) → −1 → смерть.
            var sim = TestUtil.MakeSim(maxCmd: 12, initE: 20, lightMode: 1, lightLevel: 0f,
                                       corpseFood: 23, foodDecay: 0);
            try
            {
                sim.TestSetupCells(new GpuSimulation.TestCellSpec
                {
                    x = 8, y = 8, dna = PhotoDna(), genes = 0, energy = 20
                });

                sim.TestStep();
                Assert.AreEqual(SimFlags.ALIVE, sim.TestReadCells()[0].flags & SimFlags.ALIVE,
                    "После первого шага с lightLevel=0 клетка ещё жива (энергия 7)");

                sim.TestStep();
                var cell = sim.TestReadCells()[0];
                Assert.AreEqual(SimFlags.DEAD, cell.flags & SimFlags.DEAD,
                    "Без света клетка умирает от голода");
                Assert.AreEqual(23, sim.TestReadEnergy()[0],
                    "Мёртвая клетка становится едой с питательностью corpseFood");
            }
            finally { TestUtil.Cleanup(sim); }
        }

        [Test]
        public void Photosynthesis_RadialLight_CenterBrighterThanEdge()
        {
            // lightMode=0 (радиальный): в центре поля свет ярче, чем на краю.
            // Клетка в центре получает больше энергии, чем клетка в углу.
            var sim = TestUtil.MakeSim(maxCmd: 12, initE: 20, lightMode: 0, lightLevel: 1f);
            try
            {
                int cx = sim.TestMaxX / 2, cy = sim.TestMaxY / 2;
                sim.TestSetupCells(
                    new GpuSimulation.TestCellSpec { x = cx, y = cy, dna = PhotoDna(), genes = 0, energy = 20 },
                    new GpuSimulation.TestCellSpec { x = 0,  y = 0,  dna = PhotoDna(), genes = 0, energy = 20 }
                );

                sim.TestStep();
                int[] e = sim.TestReadEnergy();
                Assert.Greater(e[0], e[1],
                    "Клетка в центре поля должна фотосинтезировать сильнее, чем в углу");
            }
            finally { TestUtil.Cleanup(sim); }
        }
    }
}