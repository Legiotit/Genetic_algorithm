using UnityEngine;
using GpuSim;

namespace GpuSim.Tests
{
    // Общие помощники для модульных тестов модели: кодирование ДНК и
    // создание GpuSimulation с детерминированными параметрами (без спавна/отрисовки).
    public static class TestUtil
    {
        // 80 байт ДНК упаковываются в 20 uint (little-endian внутри uint),
        // ровно так же, как их читает gbyte() в compute-шейдере.
        public static uint[] Dna(params byte[] bytes)
        {
            uint[] dna = new uint[20];
            for (int i = 0; i < 80 && i < bytes.Length; i++)
                dna[i / 4] |= (uint)bytes[i] << ((i % 4) * 8);
            return dna;
        }

        // Заполнить все 80 байт одним повторяющимся шаблоном (удобно для
        // клеток, которые должны бесконечно исполнять одну команду).
        public static uint[] DnaRepeat(byte cmd, byte arg = 0)
        {
            var b = new byte[80];
            for (int i = 0; i < 80; i += 2) { b[i] = cmd; b[i + 1] = arg; }
            return Dna(b);
        }

        // Коды команд = grp*8 (op/8 даёт группу). См. SimStep в GpuSim.compute.
        public const byte OP_MOVE          = 0;   // grp 0
        public const byte OP_LOOK          = 8;   // grp 1
        public const byte OP_EAT           = 16;  // grp 2
        public const byte OP_CHECKENERGY   = 24;  // grp 3
        public const byte OP_HIT           = 32;  // grp 4
        public const byte OP_PHOTO         = 40;  // grp 5
        public const byte OP_BREED         = 48;  // grp 6
        public const byte OP_GOTO          = 56;  // grp 7
        public const byte OP_BREED_COLONY  = 64;  // grp 8
        public const byte OP_TRANSFER      = 72;  // grp 9
        public const byte OP_INFECTION     = 80;  // grp 10
        public const byte OP_FIRE          = 88;  // grp 11 (else-ветка)

        // Создать симуляцию 16×16 с заданными параметрами, без спавна и отрисовки.
        // Вызывающий обязан вызвать Cleanup() в конце теста.
        public static GpuSimulation MakeSim(
            int maxCmd = 1, int initE = 20, int breed = 30, int lifespan = 0,
            int lightMode = 1, float lightLevel = 1f, int foodDecay = 1,
            int corpseFood = 23, float bonus = 0.10f, int sizeX = 16, int sizeY = 16)
        {
            var go = new GameObject("GpuSimTest_" + System.Guid.NewGuid().ToString("N").Substring(0, 6));
            var sim = go.AddComponent<GpuSimulation>();
            sim.Configure(sizeX, sizeY, maxCmd, initE, breed, bonus, false);
            sim.TestCellLifespan = lifespan;
            sim.TestLightMode = lightMode;
            sim.TestLightLevel = lightLevel;
            sim.TestFoodDecay = foodDecay;
            sim.TestCorpseFood = corpseFood;
            sim.TestTargetPopulation = 0;
            sim.TestSpawnEnabled = false;
            sim.TestDraw = false;
            sim.StartSim();
            return sim;
        }

        public static void Cleanup(GpuSimulation sim)
        {
            if (sim == null) return;
            sim.StopSim();
            Object.DestroyImmediate(sim.gameObject);
        }
    }
}