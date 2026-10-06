using NUnit.Framework;
using GpuSim;

namespace GpuSim.Tests
{
    // Жизненный цикл: смерть от голода, труп как еда, ограничение lifespan,
    // истлевание трупа и возврат слота в пул.
    public class LifecycleTests
    {
        [Test]
        public void Starvation_KillsCellAndLeavesCorpse()
        {
            // ДНК — бесконечный GOTO с нулевым смещением: только расход, без дохода.
            var sim = TestUtil.MakeSim(maxCmd: 12, initE: 20, corpseFood: 23, foodDecay: 0);
            try
            {
                sim.TestSetupCells(new GpuSimulation.TestCellSpec
                {
                    x = 8, y = 8, dna = TestUtil.DnaRepeat(TestUtil.OP_GOTO, 0),
                    genes = 0, energy = 20
                });

                sim.TestStep(); // 20 − 13 = 7 — жива
                Assert.AreEqual(SimFlags.ALIVE, sim.TestReadCells()[0].flags & SimFlags.ALIVE);

                sim.TestStep(); // 7 → 0 (break) → −1 → гибель
                var cell = sim.TestReadCells()[0];
                Assert.AreEqual(SimFlags.DEAD, cell.flags & SimFlags.DEAD, "Клетка умирает от голода");
                Assert.AreEqual(23, sim.TestReadEnergy()[0], "Труп получает питательность corpseFood");

                int[] stats = sim.TestReadStats();
                Assert.AreEqual(0, stats[0], "Живых нет");
                Assert.AreEqual(1, stats[1], "Один труп (еда)");
            }
            finally { TestUtil.Cleanup(sim); }
        }

        [Test]
        public void Lifespan_KillsCellRegardlessOfEnergy()
        {
            // Клетка фотосинтезирует (энергия растёт), но lifespan=1 → гибель на 1-м шаге.
            var sim = TestUtil.MakeSim(maxCmd: 12, initE: 20, lifespan: 1, lightMode: 1, lightLevel: 1f,
                                       foodDecay: 0);
            try
            {
                sim.TestSetupCells(new GpuSimulation.TestCellSpec
                {
                    x = 8, y = 8, dna = TestUtil.DnaRepeat(TestUtil.OP_PHOTO, 0),
                    genes = 0, energy = 20
                });

                sim.TestStep();
                var cell = sim.TestReadCells()[0];
                Assert.AreEqual(SimFlags.DEAD, cell.flags & SimFlags.DEAD,
                    "При steps≥lifespan клетка умирает даже с энергией");
                // Энергия сброшена в corpseFood, а не сохранена от фотосинтеза.
                Assert.AreEqual(23, sim.TestReadEnergy()[0]);
            }
            finally { TestUtil.Cleanup(sim); }
        }

        [Test]
        public void Corpse_DecaysOverTimeAndFreesSlot()
        {
            // Труп с corpseFood=23 и foodDecay=5: 23→18→13→8→3→(≤0, освобождение).
            var sim = TestUtil.MakeSim(maxCmd: 1, initE: 0, foodDecay: 5, corpseFood: 23);
            try
            {
                sim.TestSetupCells(new GpuSimulation.TestCellSpec
                {
                    x = 8, y = 8, dna = TestUtil.DnaRepeat(TestUtil.OP_GOTO, 0),
                    genes = 0, energy = 23, extraFlags = SimFlags.DEAD
                });

                // 4 шага: энергия ещё положительна (3), труп остаётся едой.
                for (int i = 0; i < 4; i++) sim.TestStep();
                int e = sim.TestReadEnergy()[0];
                Assert.AreEqual(3, e, "После 4 шагов decay 23−4*5=3 — труп ещё есть");
                Assert.AreEqual(1, sim.TestReadStats()[1], "Это всё ещё еда");
                // Клетка одна → её id=0, поэтому grid[slot] хранит 0 (а не позицию 8).
                Assert.AreEqual(0, sim.TestReadGrid()[8 * 16 + 8], "Слот всё ещё занят трупом (id=0)");

                // 5-й шаг: 3 − 5 = −2 ≤ 0 → труп истлевает, слот освобождается.
                sim.TestStep();
                Assert.AreEqual(-1, sim.TestReadGrid()[8 * 16 + 8], "Истлевший труп освобождает слот");
                Assert.AreEqual(SimFlags.FREE, sim.TestReadCells()[0].flags & SimFlags.FREE,
                    "Клетка возвращена в пул как FREE");
                Assert.Greater(sim.TestReadStats()[8], 0, "Освобождение слота учтено в статистике");
            }
            finally { TestUtil.Cleanup(sim); }
        }
    }
}