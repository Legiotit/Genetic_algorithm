using System;
using UnityEngine;
using UnityEngine.Rendering;
using GpuSim;

namespace GpuSim
{
    // Снимок статистики симуляции. avg-поля считаются на CPU из сумм GPU.
    [Serializable]
    public struct StatsSnapshot
    {
        public int step;
        public int alive;
        public int food;
        public int totalEnergy;
        public int births;       // за последний посчитанный шаг
        public int deaths;       // за последний посчитанный шаг
        public int colony;
        public int totalAge;
        public int freeSlots;

        public float avgEnergy => alive > 0 ? (float)totalEnergy / alive : 0f;
        public float avgAge    => alive > 0 ? (float)totalAge / alive : 0f;
    }

    // Асинхронно и редко читает GPU-буфер статистики, чтобы не тормозить симуляцию.
    // Полностью отключаемый: Collecting=false → не шлёт readback-запросов.
    // Вешается на любой GameObject; сам находит GpuSimulation, если ссылка не задана.
    public class StatsCollector : MonoBehaviour
    {
        [Tooltip("Симуляция. Если пусто — ищется автоматически.")]
        [SerializeField] private GpuSimulation gpuSim;

        [Tooltip("Интервал обновления в кадрах. Больше = реже и дешевле.")]
        [Range(1, 600)] public int intervalFrames = 60;

        [Tooltip("Собирать статистику? Можно переключать в рантайме.")]
        public bool Collecting = true;

        // Последний готовый снимок (доступен всегда, даже между обновлениями).
        public StatsSnapshot Latest { get; private set; }

        // Срабатывает, когда пришёл новый снимок с GPU.
        public event Action<StatsSnapshot> OnStatsUpdated;

        private int[] _data = new int[9];
        private int _lastFrame = -99999;
        private bool _pending;

        void Awake()
        {
            if (gpuSim == null) gpuSim = FindFirstObjectByType<GpuSimulation>();
        }

        void Update()
        {
            if (!Collecting || gpuSim == null || !gpuSim.IsRunning) return;
            if (gpuSim.StatsBuffer == null) return;
            if (_pending) return; // ждём завершения предыдущего readback

            if (Time.frameCount - _lastFrame < intervalFrames) return;
            _lastFrame = Time.frameCount;
            _pending = true;

            // Перегрузка без size/offset читает весь буфер целиком.
            AsyncGPUReadback.Request(gpuSim.StatsBuffer, OnReadback);
        }

        void OnReadback(AsyncGPUReadbackRequest r)
        {
            _pending = false;
            if (r.hasError || !r.done) return;

            var native = r.GetData<int>();
            for (int i = 0; i < _data.Length && i < native.Length; i++) _data[i] = native[i];

            Latest = new StatsSnapshot
            {
                step       = _data[2],
                alive      = _data[0],
                food       = _data[1],
                totalEnergy= _data[3],
                births     = _data[4],
                deaths     = _data[5],
                colony     = _data[6],
                totalAge   = _data[7],
                freeSlots  = _data[8],
            };
            OnStatsUpdated?.Invoke(Latest);
        }
    }
}