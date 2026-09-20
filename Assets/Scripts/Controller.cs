using UnityEngine;
using UnityEngine.UI;
using GpuSim;

// Контроллер UI: задаёт размер поля и начальные настройки, запускает GPU-симуляцию.
// Имена методов (CreateMap/ViewSelect/ModeSelect/Pause) сохранены, чтобы
// существующие onClick-привязки кнопок в сцене остались рабочими.
public class Controller : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private InputField inputX;
    [SerializeField] private InputField inputY;
    [SerializeField] private Text text;

    [Header("GPU-симуляция")]
    [SerializeField] private GpuSimulation gpuSim;

    [Header("Настройки")]
    [Tooltip("Если задано — используется вместо значений из InputField.")]
    [SerializeField] private SimSettings settings;

    [Header("Начальные настройки (fallback, если settings пусто)")]
    [SerializeField] private int maxCommand = 25;
    [SerializeField] private int initialEnergy = 20;
    [SerializeField] private int breedCost = 30;
    [SerializeField] private int targetPopulation = 4000;
    [SerializeField] private float statBonus = 0.10f;
    [SerializeField] private bool draw = true;

    void Awake()
    {
        if (gpuSim == null)
            gpuSim = FindFirstObjectByType<GpuSimulation>();
    }

    // Кнопка Start
    public void CreateMap()
    {
        if (gpuSim == null) return;
        if (settings != null)
        {
            // Поля ввода переопределяют размеры, если заполнены.
            if (inputX != null && int.TryParse(inputX.text, out int sx)) settings.sizeX = Mathf.Clamp(sx, 8, 2048);
            if (inputY != null && int.TryParse(inputY.text, out int sy)) settings.sizeY = Mathf.Clamp(sy, 8, 2048);
            gpuSim.Configure(settings);
        }
        else
        {
            int x = Parse(inputX, 128);
            int y = Parse(inputY, 128);
            gpuSim.Configure(x, y, maxCommand, initialEnergy, breedCost, statBonus, draw);
        }
        gpuSim.StartSim();
    }

    // Кнопка View — переключить отрисовку
    public void ViewSelect()
    {
        if (gpuSim != null) gpuSim.ToggleDraw();
    }

    // Кнопка Mode — переключить режим цвета
    public void ModeSelect()
    {
        if (gpuSim != null) gpuSim.ToggleViewMode();
    }

    // Кнопка Pause — пауза/возобновление
    public void Pause()
    {
        if (gpuSim != null) gpuSim.TogglePause();
    }

    void Update()
    {
        if (text == null || gpuSim == null) return;
        if (gpuSim.IsRunning)
            text.text = $"step {gpuSim.Step}  alive {gpuSim.AliveCount}  food {gpuSim.FoodCount}";
        else
            text.text = "остановлено";
    }

    static int Parse(InputField f, int def)
    {
        if (f == null) return def;
        int v;
        return int.TryParse(f.text, out v) ? Mathf.Clamp(v, 8, 2048) : def;
    }
}