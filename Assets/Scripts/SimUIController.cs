using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using GpuSim;

namespace GpuSim
{
    // Привязывает логику к готовому UI-префабу «SimUI».
    // Сам ничего не строит: элементы находятся по их структуре/именам в Start(),
    // поэтому визуал префаба можно свободно редактировать в Inspector.
    // Компонент вешается на корень префаба (тот же объект, где Canvas).
    public class SimUIController : MonoBehaviour
    {
        [Header("Связи (опционально, ищутся автоматически)")]
        [SerializeField] private GpuSimulation gpuSim;
        [SerializeField] private StatsCollector statsCollector;
        [SerializeField] private MoveCamera moveCamera;
        [SerializeField] private SimSettings settings;

        // Найденные элементы UI.
        private Text _statusText;
        private Text _statsText;
        private InputField _inSizeX, _inSizeY;

        void Awake()
        {
            if (gpuSim == null) gpuSim = FindFirstObjectByType<GpuSimulation>();
            if (moveCamera == null) moveCamera = FindFirstObjectByType<MoveCamera>();
            if (statsCollector == null) statsCollector = FindFirstObjectByType<StatsCollector>();
            if (settings == null) settings = new SimSettings();
        }

        void Start()
        {
            if (statsCollector == null)
                statsCollector = gameObject.AddComponent<StatsCollector>();
            statsCollector.OnStatsUpdated += OnStats;

            BindAll();
        }

        void OnDestroy()
        {
            if (statsCollector != null) statsCollector.OnStatsUpdated -= OnStats;
        }

        // ---------- Привязка элементов префаба ----------

        void BindAll()
        {
            _statusText = Find<Text>("TopBar/Text");
            _statsText = Find<Text>("StatsPanel/Text");

            Transform settingsPanel = transform.Find("SettingsPanel");
            Transform displayPanel = transform.Find("DisplayPanel");
            Transform statsPanel = transform.Find("StatsPanel");

            if (settingsPanel != null) BindSettings(settingsPanel);
            if (displayPanel != null) BindDisplay(displayPanel);
            if (statsPanel != null) BindStats(statsPanel);
        }

        void BindSettings(Transform panel)
        {
            // Поля размера — в первом Row панели (Row размеров идёт первым).
            var sizeRow = panel.Find("Row");
            if (sizeRow != null)
            {
                var inputs = sizeRow.GetComponentsInChildren<InputField>(true);
                if (inputs.Length >= 2) { _inSizeX = inputs[0]; _inSizeY = inputs[1]; }
                if (_inSizeX != null) _inSizeX.text = settings.sizeX.ToString();
                if (_inSizeY != null) _inSizeY.text = settings.sizeY.ToString();
            }

            // Слайдеры: родительский объект имеет имя "SldRow_<label>".
            BindSlider(panel, "Команд за шаг",       () => settings.maxCommand,    v => settings.maxCommand = (int)v, true);
            BindSlider(panel, "Старт. энергия",      () => settings.initialEnergy, v => settings.initialEnergy = (int)v, true);
            BindSlider(panel, "Цена размножения",   () => settings.breedCost,     v => settings.breedCost = (int)v, true);
            BindSlider(panel, "Время жизни (0=∞)",  () => settings.cellLifespan,  v => settings.cellLifespan = (int)v, true);
            BindSlider(panel, "Режим света",        () => settings.lightMode,     v => settings.lightMode = (int)v, true);
            BindSlider(panel, "Уровень света",      () => settings.lightLevel,    v => settings.lightLevel = v, false);
            BindSlider(panel, "Убыль еды",          () => settings.foodDecay,     v => settings.foodDecay = (int)v, true);
            BindSlider(panel, "Бонус генов",        () => settings.statBonus,     v => settings.statBonus = v, false);
            BindSlider(panel, "Целевая популяция",   () => settings.targetPopulation, v => settings.targetPopulation = (int)v, true);
            BindSlider(panel, "Спавн/кадр",         () => settings.spawnPerFrame, v => settings.spawnPerFrame = (int)v, true);

            // Тоглы: родитель "TglRow_<label>".
            BindToggle(panel, "Поддерживать популяцию", () => settings.spawnEnabled, v => settings.spawnEnabled = v);
            BindToggle(panel, "Автозапуск",          () => settings.autoStart,    v => settings.autoStart = v);
            BindToggle(panel, "Отрисовка",           () => settings.draw,         v => settings.draw = v);

            // Кнопки по имени (Btn_Старт и т.д.) — ищем во всей панели.
            BindButton(panel, "Btn_Старт", OnStart);
            BindButton(panel, "Btn_Стоп", OnStop);
            BindButton(panel, "Btn_Применить", OnApply);
        }

        void BindDisplay(Transform panel)
        {
            BindButton(panel, "Btn_Пауза", OnPause);
            BindButton(panel, "Btn_Шаг", OnStep);
            BindButton(panel, "Btn_Вписать", OnFrame);
            BindButton(panel, "Btn_ДНК", () => SetMode(0));
            BindButton(panel, "Btn_Энергия", () => SetMode(1));
            BindButton(panel, "Btn_Возраст", () => SetMode(2));
            BindButton(panel, "Btn_Действие", () => SetMode(3));
            BindButton(panel, "Btn_Колония", () => SetMode(4));

            // Слайдер скорости.
            var sSpeed = FindSliderByRow(panel, "Скорость (шагов/кадр)");
            if (sSpeed != null)
            {
                var txt = sSpeed.transform.parent.Find("Text")?.GetComponent<Text>();
                sSpeed.onValueChanged.AddListener(v =>
                {
                    if (gpuSim != null) gpuSim.SetSpeed((int)v);
                    if (txt != null) txt.text = $"Скорость (шагов/кадр): {(int)v}";
                });
                sSpeed.value = 1;
            }
        }

        void BindStats(Transform panel)
        {
            BindToggle(panel, "Собирать статистику",
                () => statsCollector != null && statsCollector.Collecting,
                v => { if (statsCollector != null) statsCollector.Collecting = v; });

            var sInterval = FindSliderByRow(panel, "Интервал (кадры)");
            if (sInterval != null && statsCollector != null)
            {
                var txt = sInterval.transform.parent.Find("Text")?.GetComponent<Text>();
                sInterval.onValueChanged.AddListener(v =>
                {
                    if (statsCollector != null) statsCollector.intervalFrames = (int)v;
                    if (txt != null) txt.text = $"Интервал (кадры): {(int)v}";
                });
                sInterval.value = statsCollector.intervalFrames;
            }
        }

        // ---------- Хелперы поиска ----------

        T Find<T>(string path) where T : Component
        {
            var t = transform.Find(path);
            if (t == null) { Debug.LogWarning($"[SimUI] не найден путь: {path}"); return null; }
            return t.GetComponent<T>();
        }

        // Слайдер по имени строки-родителя "SldRow_<rowName>".
        Slider FindSliderByRow(Transform panel, string rowName)
        {
            foreach (var s in panel.GetComponentsInChildren<Slider>(true))
                if (s.transform.parent != null && s.transform.parent.name == "SldRow_" + rowName)
                    return s;
            Debug.LogWarning($"[SimUI] слайдер не найден: {rowName}");
            return null;
        }

        void BindSlider(Transform panel, string rowName,
            System.Func<float> get, System.Action<float> set, bool whole)
        {
            var s = FindSliderByRow(panel, rowName);
            if (s == null) return;
            var txt = s.transform.parent.Find("Text")?.GetComponent<Text>();
            System.Action<float> upd = v =>
            {
                set(v);
                if (txt != null) txt.text = $"{rowName}: {(whole ? ((int)v).ToString() : v.ToString("F2"))}";
            };
            s.onValueChanged.AddListener(v => upd(v));
            s.value = get();
            upd(s.value);
        }

        void BindToggle(Transform panel, string rowName,
            System.Func<bool> get, System.Action<bool> set)
        {
            Toggle t = null;
            foreach (var tg in panel.GetComponentsInChildren<Toggle>(true))
                if (tg.transform.parent != null && tg.transform.parent.name == "TglRow_" + rowName) { t = tg; break; }
            if (t == null) { Debug.LogWarning($"[SimUI] тогл не найден: {rowName}"); return; }
            t.onValueChanged.AddListener(v => set(v));
            t.isOn = get();
        }

        void BindButton(Transform panel, string btnName, UnityEngine.Events.UnityAction onClick)
        {
            Button b = null;
            foreach (var btn in panel.GetComponentsInChildren<Button>(true))
                if (btn.name == btnName) { b = btn; break; }
            if (b == null) { Debug.LogWarning($"[SimUI] кнопка не найдена: {btnName}"); return; }
            b.onClick.AddListener(onClick);
        }

        // ---------- Обработчики ----------

        void OnStart()
        {
            if (gpuSim == null) return;
            if (_inSizeX != null && int.TryParse(_inSizeX.text, out int x)) settings.sizeX = Mathf.Clamp(x, 8, 2048);
            if (_inSizeY != null && int.TryParse(_inSizeY.text, out int y)) settings.sizeY = Mathf.Clamp(y, 8, 2048);
            gpuSim.Configure(settings);
            gpuSim.StartSim();
        }

        void OnStop()
        {
            if (gpuSim != null) gpuSim.StopSim();
        }

        void OnApply()
        {
            if (gpuSim == null) return;
            gpuSim.Configure(settings);
            if (gpuSim.IsRunning) gpuSim.ApplyRuntimeParams();
        }

        void OnPause()
        {
            if (gpuSim != null) gpuSim.TogglePause();
        }

        void OnStep()
        {
            if (gpuSim != null) gpuSim.StepOnce();
        }

        void OnFrame()
        {
            if (moveCamera != null) moveCamera.Frame();
        }

        void SetMode(int mode)
        {
            if (gpuSim != null) gpuSim.SetViewMode(mode);
        }

        void OnStats(StatsSnapshot s)
        {
            if (_statsText != null)
            {
                _statsText.text =
                    $"шаг {s.step}   живых {s.alive}   еды {s.food}\n" +
                    $"колония {s.colony}   своб.слотов {s.freeSlots}\n" +
                    $"рождений {s.births}   смертей {s.deaths}\n" +
                    $"∑энергии {s.totalEnergy}   ср.энергия {s.avgEnergy:F1}   ср.возраст {s.avgAge:F1}";
            }
        }

        void Update()
        {
            if (_statusText != null && gpuSim != null)
            {
                string state = !gpuSim.IsRunning ? "остановлено" : gpuSim.IsPaused ? "пауза" : "работает";
                _statusText.text = $"  {state}   шаг {gpuSim.Step}   режим {ModeName(gpuSim.ViewMode)}   скорость {gpuSim.StepsPerFrame}×";
            }
        }

        static string ModeName(int m)
        {
            switch (m)
            {
                case 0: return "ДНК";
                case 1: return "энергия";
                case 2: return "возраст";
                case 3: return "действие";
                case 4: return "колония";
                default: return "?";
            }
        }
    }
}