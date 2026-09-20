using System;
using UnityEngine;

namespace GpuSim
{
    // Состояние клетки. Энергия вынесена в отдельный буфер (_Energy),
    // чтобы по ней можно было делать InterlockedAdd без гонки с полным записью структуры.
    [Serializable]
    public struct CellState
    {
        public int x, y;     // позиция на сетке
        public int dir;      // направление 0..7
        public int ip;       // указатель команд в ДНК (сохраняется между шагами)
        public int steps;   // возраст
        public int id;       // = индекс в пуле
        public uint flags;   // биты SimFlags
        public int pad;      // выравнивание до 32 байт
    }

    // Намерение одной клетки: не более одного fallible-действия за шаг.
    [Serializable]
    public struct Intent
    {
        public int type;        // 0 none,1 move,2 eat,3 hit,4 breed,5 breedColony,6 transfer,7 infection,8 fire
        public int targetSlot;  // ny*MaxX+nx
        public int oldSlot;     // для move
        public int amount;      // базовый урон/энергия
        public int ipAfter;     // IP после размера команды
        public int priority;    // приоритет (инициатива-сила)
        public int okOffset;    // переход при успехе
        public int failOffset;  // переход при неудаче
        public int childIp;     // стартовый IP потомка (breed)
        public int extra;
    }

    public static class SimFlags
    {
        public const uint ALIVE    = 1u << 0; // живая, исполняет ДНК
        public const uint DEAD     = 1u << 1; // мёртвая = еда
        public const uint COLONY   = 1u << 2; // часть колонии
        public const uint PENDING  = 1u << 3; // есть незавершённое намерение
        public const uint DNADIRTY = 1u << 4; // ДНК нужно перезаписать (инфекция)
        public const uint FREE     = 1u << 5; // слот пула свободен
        public const uint INPOOL   = 1u << 6; // клетка лежит в пуле свободных (_FreeList)
    }

    public enum CmdType
    {
        None = 0, Move = 1, Eat = 2, Hit = 3, Breed = 4,
        BreedColony = 5, Transfer = 6, Infection = 7, Fire = 8
    }
}