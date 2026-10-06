using UnityEngine;

namespace GpuSim
{
    // Все настраиваемые параметры симуляции в одном месте.
    // Кладётся на тот же GameObject, что и Controller (или GpuSimulation),
    // инспектируется в Inspector и передаётся в GpuSimulation.Configure().
    [System.Serializable]
    public class SimSettings
    {
        [Header("Размеры поля")]
        [Range(8, 2048)] public int sizeX = 128;
        [Range(8, 2048)] public int sizeY = 128;

        [Header("Клетка")]
        [Tooltip("Макс. число команд, исполняемых клеткой за один шаг.")]
        [Range(1, 64)] public int maxCommand = 12;
        [Tooltip("Стартовая энергия новой клетки.")]
        [Range(1, 255)] public int initialEnergy = 20;
        [Tooltip("Порог энергии для размножения.")]
        [Range(1, 255)] public int breedCost = 30;
        [Tooltip("Время жизни клетки в шагах. 0 = бессмертна (умирает только от голода).")]
        [Range(0, 10000)] public int cellLifespan = 1000;

        [Header("Свет / фотосинтез")]
        [Tooltip("Режим распределения света: 0=радиальное от центра, 1=равномерное, 2=градиент по Y.")]
        [Range(0, 2)] public int lightMode = 0;
        [Tooltip("Множитель мощности фотосинтеза.")]
        [Range(0f, 2f)] public float lightLevel = 1f;

        [Header("Экономика")]
        [Tooltip("Убыль энергии мёртвой клетки (еды) за шаг.")]
        [Range(0, 20)] public int foodDecay = 1;
        [Tooltip("Стартовая питательность свежего трупа; каждый шаг убывает на foodDecay, пока не истает. Поедание даёт текущее значение.")]
        [Range(0, 255)] public int corpseFood = 23;
        [Tooltip("Вклад разницы генов в бой/защиту/эффективность.")]
        [Range(0f, 0.5f)] public float statBonus = 0.10f;

        [Header("Население")]
        [Tooltip("Целевая популяция при первичном спавне.")]
        [Range(0, 100000)] public int targetPopulation = 4000;
        [Tooltip("Сколько клеток спавнить за кадр (поддержание популяции).")]
        [Range(0, 1000)] public int spawnPerFrame = 60;
        public bool spawnEnabled = true;

        [Header("Запуск")]
        public bool autoStart = false;
        public bool draw = true;
    }
}