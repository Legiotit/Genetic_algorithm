using NUnit.Framework;
using GpuSim;

namespace GpuSim.Tests
{
    // Проверка отдельных действий клетки: MOVE, EAT, HIT, TRANSFER,
    // BREED, INFECTION, FIRE, GOTO, CHECKENERGY. Каждая команда тестируется
    // изолированно (maxCommand=1 → ровно одна команда за шаг).
    //
    // Направления DD[8]: 0=+Y, 2=+X (восток), 4=−Y, 6=−X.
    public class CellActionTests
    {
        // ---- MOVE ----
        [Test]
        public void Move_IntoEmptySlot_ChangesPositionAndGrid()
        {
            var sim = TestUtil.MakeSim(maxCmd: 1, initE: 20);
            try
            {
                sim.TestSetupCells(new GpuSimulation.TestCellSpec
                {
                    x = 5, y = 5, dir = 2, ip = 0,        // смотрит на восток (+X)
                    dna = TestUtil.Dna(TestUtil.OP_MOVE, 0, 0),
                    genes = 0, energy = 20
                });

                sim.TestStep();
                var cell = sim.TestReadCells()[0];
                Assert.AreEqual(6, cell.x, "Клетка сдвинулась на +X");
                Assert.AreEqual(5, cell.y);

                int[] grid = sim.TestReadGrid();
                Assert.AreEqual(0, grid[5 * 16 + 6], "Новый слот занят клеткой");
                Assert.AreEqual(-1, grid[5 * 16 + 5], "Старый слот свободен");
            }
            finally { TestUtil.Cleanup(sim); }
        }

        [Test]
        public void Move_IntoOccupiedSlot_DoesNotMove()
        {
            var sim = TestUtil.MakeSim(maxCmd: 1, initE: 20);
            try
            {
                sim.TestSetupCells(
                    new GpuSimulation.TestCellSpec { x = 5, y = 5, dir = 2, dna = TestUtil.Dna(TestUtil.OP_MOVE, 0, 0), genes = 0, energy = 20 },
                    new GpuSimulation.TestCellSpec { x = 6, y = 5, dir = 0, dna = TestUtil.Dna(TestUtil.OP_GOTO, 0),     genes = 0, energy = 20 }
                );

                sim.TestStep();
                var cell = sim.TestReadCells()[0];
                Assert.AreEqual(5, cell.x, "Клетка не может войти в занятый слот");
                Assert.AreEqual(5, cell.y);
            }
            finally { TestUtil.Cleanup(sim); }
        }

        // ---- EAT ----
        [Test]
        public void Eat_DeadCell_GainsItsFoodAndClearsSlot()
        {
            var sim = TestUtil.MakeSim(maxCmd: 1, initE: 20, corpseFood: 23);
            try
            {
                sim.TestSetupCells(
                    // eater смотрит на восток, где лежит труп
                    new GpuSimulation.TestCellSpec { x = 5, y = 5, dir = 2, dna = TestUtil.Dna(TestUtil.OP_EAT, 0, 0), genes = 0, energy = 20 },
                    // труп (DEAD, не ALIVE) с питательностью corpseFood
                    new GpuSimulation.TestCellSpec { x = 6, y = 5, dir = 0, dna = TestUtil.Dna(TestUtil.OP_GOTO, 0), genes = 0, energy = 23, extraFlags = SimFlags.DEAD }
                );

                sim.TestStep();
                int[] e = sim.TestReadEnergy();
                // Симшаг: 20 − cost(2)=2 − базовый 1 = 17; Apply: +23 (еда) → 40.
                Assert.AreEqual(40, e[0], "Поедание трупа должно добавить его питательность");

                int[] grid = sim.TestReadGrid();
                Assert.AreEqual(-1, grid[5 * 16 + 6], "Съеденный труп освобождает слот");
            }
            finally { TestUtil.Cleanup(sim); }
        }

        // ---- HIT ----
        [Test]
        public void Hit_LivingNeighbour_DamagesTarget()
        {
            var sim = TestUtil.MakeSim(maxCmd: 1, initE: 50, corpseFood: 23);
            try
            {
                sim.TestSetupCells(
                    new GpuSimulation.TestCellSpec { x = 5, y = 5, dir = 2, dna = TestUtil.Dna(TestUtil.OP_HIT, 0, 0), genes = 0, energy = 50 },
                    new GpuSimulation.TestCellSpec { x = 6, y = 5, dir = 0, dna = TestUtil.Dna(TestUtil.OP_GOTO, 0),   genes = 0, energy = 50 }
                );

                sim.TestStep();
                var cells = sim.TestReadCells();
                int[] e = sim.TestReadEnergy();
                // Урон = ceil(60 * dealtMult(0) * takenMult(0)) = 60. 50 − 60 = −10 → смерть.
                Assert.AreEqual(SimFlags.DEAD, cells[1].flags & SimFlags.DEAD,
                    "Жертва с 50 энергии гибнет от удара 60");
                Assert.AreEqual(23, e[1], "Убитая клетка становится трупом с энергией corpseFood");
            }
            finally { TestUtil.Cleanup(sim); }
        }

        // ---- TRANSFER ----
        [Test]
        public void Transfer_MovesEnergyToNeighbour()
        {
            var sim = TestUtil.MakeSim(maxCmd: 1, initE: 30);
            try
            {
                sim.TestSetupCells(
                    new GpuSimulation.TestCellSpec { x = 5, y = 5, dir = 2, dna = TestUtil.Dna(TestUtil.OP_TRANSFER, 0, 0), genes = 0, energy = 30 },
                    new GpuSimulation.TestCellSpec { x = 6, y = 5, dir = 0, dna = TestUtil.Dna(TestUtil.OP_GOTO, 0),         genes = 0, energy = 10 }
                );

                sim.TestStep();
                int[] e = sim.TestReadEnergy();
                // Симшаг: food=30/5=6; 30−6 −cost(1)=1 −базовый 1 = 22 (донор).
                // Получатель тоже действует (GOTO): 10 − 1 − 1 = 8.
                // Apply: получатель += (6 − 3) = 3 → 8 + 3 = 11.
                Assert.AreEqual(22, e[0], "Донор теряет переданную долю плюс расход");
                Assert.AreEqual(11, e[1], "Получатель получает долю минус накладные 3");
            }
            finally { TestUtil.Cleanup(sim); }
        }

        // ---- BREED ----
        [Test]
        public void Breed_WithEnoughEnergy_SpawnsChildInTargetSlot()
        {
            var sim = TestUtil.MakeSim(maxCmd: 1, initE: 100, breed: 30, corpseFood: 23);
            try
            {
                // childIp = 5 (byte[1]); ok/fail offsets = 0.
                sim.TestSetupCells(new GpuSimulation.TestCellSpec
                {
                    x = 5, y = 5, dir = 2, ip = 0,
                    dna = TestUtil.Dna(TestUtil.OP_BREED, 5, 0, 0),
                    genes = 0, energy = 100
                });

                sim.TestStep();

                int[] grid = sim.TestReadGrid();
                int childId = grid[5 * 16 + 6];
                Assert.GreaterOrEqual(childId, 1, "В целевом слоте появился потомок");

                var cells = sim.TestReadCells();
                int[] e = sim.TestReadEnergy();
                Assert.AreEqual(SimFlags.ALIVE, cells[childId].flags & SimFlags.ALIVE,
                    "Потомок живой");
                Assert.AreEqual(6, cells[childId].x);
                Assert.AreEqual(5, cells[childId].y);
                Assert.AreEqual(5, cells[childId].ip, "Стартовый IP потомка = childIp из ДНК родителя");

                int give = MathfCeil(100 * 0.45f); // 45
                Assert.AreEqual(give, e[childId], "Потомок получает ~45% энергии родителя");

                // Родитель: 100 − (give+5) − cost(5) − базовый 1 = 100 − 45 − 5 − 5 − 1 = 44.
                Assert.AreEqual(44, e[0], "Родитель тратит энергию на потомка и команду");

                int[] stats = sim.TestReadStats();
                Assert.AreEqual(2, stats[0], "Две живые клетки (родитель + потомок)");
                Assert.AreEqual(1, stats[4], "Зарегистрировано одно рождение");
            }
            finally { TestUtil.Cleanup(sim); }
        }

        [Test]
        public void Breed_WithoutEnoughEnergy_DoesNotSpawn()
        {
            var sim = TestUtil.MakeSim(maxCmd: 1, initE: 10, breed: 30);
            try
            {
                sim.TestSetupCells(new GpuSimulation.TestCellSpec
                {
                    x = 5, y = 5, dir = 2, ip = 0,
                    dna = TestUtil.Dna(TestUtil.OP_BREED, 0, 0, 0),
                    genes = 0, energy = 10
                });

                sim.TestStep();
                int[] grid = sim.TestReadGrid();
                Assert.AreEqual(-1, grid[5 * 16 + 6], "При энергии ≤ breedCost потомок не создаётся");
                Assert.AreEqual(0, sim.TestReadStats()[4], "Рождения не зафиксировано");
            }
            finally { TestUtil.Cleanup(sim); }
        }

        // ---- INFECTION ----
        [Test]
        public void Infection_OverwritesTargetDnaWithAttackerDna()
        {
            var sim = TestUtil.MakeSim(maxCmd: 1, initE: 50);
            try
            {
                uint[] attackerDna = TestUtil.Dna(TestUtil.OP_INFECTION, 0, 0);
                uint[] victimDna   = TestUtil.DnaRepeat(TestUtil.OP_GOTO, 0);
                sim.TestSetupCells(
                    new GpuSimulation.TestCellSpec { x = 5, y = 5, dir = 2, dna = attackerDna, genes = 0, energy = 50 },
                    new GpuSimulation.TestCellSpec { x = 6, y = 5, dir = 0, dna = victimDna,   genes = 0, energy = 50 }
                );

                sim.TestStep();
                uint[] victimNow = sim.TestReadDna(1);
                CollectionAssert.AreEqual(attackerDna, victimNow,
                    "Заражённая клетка получает ДНК атакующего");
            }
            finally { TestUtil.Cleanup(sim); }
        }

        // ---- FIRE ----
        [Test]
        public void Fire_HitsFirstOccupiedCellInRange()
        {
            var sim = TestUtil.MakeSim(maxCmd: 1, initE: 50, corpseFood: 23);
            try
            {
                // Жертва стоит в 1 клетке на восток — попадает в первый же занятый слот.
                sim.TestSetupCells(
                    new GpuSimulation.TestCellSpec { x = 5, y = 5, dir = 2, dna = TestUtil.Dna(TestUtil.OP_FIRE, 0, 0), genes = 0, energy = 50 },
                    new GpuSimulation.TestCellSpec { x = 6, y = 5, dir = 0, dna = TestUtil.Dna(TestUtil.OP_GOTO, 0),     genes = 0, energy = 50 }
                );

                sim.TestStep();
                int[] e = sim.TestReadEnergy();
                // Жертва тоже действует за шаг: GOTO −1 (команда) −1 (база) = 50 → 48.
                // Урон = ceil(35 * 1 * 1) = 35 → 48 − 35 = 13 (жива).
                Assert.AreEqual(13, e[1], "Огонь наносит 35 урона ближайшей цели");
            }
            finally { TestUtil.Cleanup(sim); }
        }

        // ---- GOTO ----
        [Test]
        public void Goto_SetsInstructionPointer()
        {
            var sim = TestUtil.MakeSim(maxCmd: 1, initE: 50);
            try
            {
                sim.TestSetupCells(new GpuSimulation.TestCellSpec
                {
                    x = 5, y = 5, ip = 0,
                    dna = TestUtil.Dna(TestUtil.OP_GOTO, 10), // безусловный переход на 10
                    genes = 0, energy = 50
                });

                sim.TestStep();
                Assert.AreEqual(10, sim.TestReadCells()[0].ip,
                    "GOTO устанавливает IP в значение следующего байта");
            }
            finally { TestUtil.Cleanup(sim); }
        }

        // ---- CHECKENERGY (развилка) ----
        [Test]
        public void CheckEnergy_AboveThreshold_TakesTrueBranch()
        {
            var sim = TestUtil.MakeSim(maxCmd: 1, initE: 50);
            try
            {
                // thr=20, trueOff=5, falseOff=9. energy=50 > 20 → off=5 → ip=(0+4+5)=9.
                sim.TestSetupCells(new GpuSimulation.TestCellSpec
                {
                    x = 5, y = 5, ip = 0,
                    dna = TestUtil.Dna(TestUtil.OP_CHECKENERGY, 20, 5, 9),
                    genes = 0, energy = 50
                });

                sim.TestStep();
                Assert.AreEqual(9, sim.TestReadCells()[0].ip, "При energy>thr переход по true-ветке");
            }
            finally { TestUtil.Cleanup(sim); }
        }

        [Test]
        public void CheckEnergy_BelowThreshold_TakesFalseBranch()
        {
            var sim = TestUtil.MakeSim(maxCmd: 1, initE: 10);
            try
            {
                // thr=20, energy=10 ≤ 20 → off=9 → ip=(0+4+9)=13.
                sim.TestSetupCells(new GpuSimulation.TestCellSpec
                {
                    x = 5, y = 5, ip = 0,
                    dna = TestUtil.Dna(TestUtil.OP_CHECKENERGY, 20, 5, 9),
                    genes = 0, energy = 10
                });

                sim.TestStep();
                Assert.AreEqual(13, sim.TestReadCells()[0].ip, "При energy≤thr переход по false-ветке");
            }
            finally { TestUtil.Cleanup(sim); }
        }

        // ---- LOOK (поворот + развилка на 5 категорий) ----
        [Test]
        public void Look_SeesEnemyAhead_AndBranches()
        {
            var sim = TestUtil.MakeSim(maxCmd: 1, initE: 50);
            try
            {
                // LOOK: byte[ip+1..5] = смещения для cat 0..4. cat=1 (враг) при чужой ДНК.
                // Сосед впереди (восток) — чужая клетка с другой ДНК → cat=1 → off=byte[2].
                // ip = (0 + 6 + off) % 80. Зададим off для cat1 = 7 → ip = 13.
                byte[] dna = new byte[80];
                dna[0] = TestUtil.OP_LOOK;          // grp1, op%8=0 → поворот на dir+0
                dna[1] = 0;                          // cat0 (пусто)
                dna[2] = 7;                          // cat1 (враг)
                dna[3] = 0;                          // cat2 (друг)
                dna[4] = 0;                          // cat3 (труп)
                dna[5] = 0;                          // cat4 (зарезервировано)

                uint[] enemyDna = TestUtil.DnaRepeat(TestUtil.OP_PHOTO, 99); // точно чужая
                sim.TestSetupCells(
                    new GpuSimulation.TestCellSpec { x = 5, y = 5, dir = 2, ip = 0, dna = TestUtil.Dna(dna), genes = 0, energy = 50 },
                    new GpuSimulation.TestCellSpec { x = 6, y = 5, dir = 0, ip = 0, dna = enemyDna,          genes = 0, energy = 50 }
                );

                sim.TestStep();
                Assert.AreEqual(13, sim.TestReadCells()[0].ip,
                    "LOOK видит врага и переходит по cat1-ветке");
            }
            finally { TestUtil.Cleanup(sim); }
        }

        static int MathfCeil(float v) => UnityEngine.Mathf.CeilToInt(v);
    }
}