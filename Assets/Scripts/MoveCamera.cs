using UnityEngine;
using GpuSim;

// Управление камерой над полем симуляции: зум колесом мыши, перетаскивание
// средней/правой кнопкой и WASD-стрелками, ограничение границами поля,
// вписывание поля кнопкой Frame. Камера ортографическая (настраивается в GpuSimulation.FrameCamera).
public class MoveCamera : MonoBehaviour
{
    [Header("Источники границ")]
    [Tooltip("Симуляция, из которой берутся размеры поля. Если пусто — ищется автоматически.")]
    [SerializeField] private GpuSimulation gpuSim;

    [Header("Управление")]
    [SerializeField] private float moveSpeed = 12f;       // скорость WASD, ед/сек
    [SerializeField] private float zoomSpeed = 0.6f;       // чувствительность колеса
    [SerializeField] private float dragSpeed = 0.02f;      // чувствительность перетаскивания
    [SerializeField] private float minZoomSize = 2f;       // макс. приближение
    [SerializeField] private float edgePadding = 1.5f;     // отступ от границ поля
    [SerializeField] private bool clampToField = true;     // ограничивать движение границами

    // Перетаскивание не должно конфликтовать с WASD-вектором, поэтому
    // флаг _dragging гасит клавиатурное перемещение на время перетаскивания.
    private Camera _cam;
    private bool _dragging;
    private Vector3 _dragLastScreen;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        if (gpuSim == null) gpuSim = FindFirstObjectByType<GpuSimulation>();
    }

    void Update()
    {
        if (_cam == null) return;

        // --- Зум колесом мыши ---
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (!Mathf.Approximately(scroll, 0f))
        {
            float size = _cam.orthographicSize - scroll * zoomSpeed * _cam.orthographicSize * 2f;
            _cam.orthographicSize = ClampZoom(size);
        }

        // --- Перетаскивание (средняя или правая кнопка) ---
        bool dragButton = Input.GetMouseButton(2) || Input.GetMouseButton(1);
        if (dragButton && !_dragging)
        {
            _dragging = true;
            _dragLastScreen = Input.mousePosition;
        }
        else if (!dragButton)
        {
            _dragging = false;
        }

        Vector3 delta = Vector3.zero;

        if (_dragging)
        {
            Vector3 screenNow = Input.mousePosition;
            Vector3 worldPrev = _cam.ScreenToWorldPoint(_dragLastScreen);
            Vector3 worldNow = _cam.ScreenToWorldPoint(screenNow);
            delta = worldPrev - worldNow; // двигаем камеру вслед за «захватом»
            _dragLastScreen = screenNow;
        }
        else
        {
            // --- WASD / стрелки ---
            float dx = Input.GetAxis("Horizontal");
            float dy = Input.GetAxis("Vertical");
            delta = new Vector3(dx, dy, 0f) * (moveSpeed * Time.deltaTime);
        }

        if (delta.sqrMagnitude > 0f)
        {
            Vector3 p = transform.position + delta;
            if (clampToField) p = ClampPosition(p, _cam.orthographicSize);
            transform.position = new Vector3(p.x, p.y, transform.position.z);
        }
    }

    // Вписать поле в экран (вызывается кнопкой Frame в UI).
    public void Frame()
    {
        if (gpuSim != null && gpuSim.IsRunning) gpuSim.FrameCamera();
        else FrameLocal();
    }

    // Локальное вписание, когда симуляция ещё не запущена: используем FieldMaxX/Y по умолчанию.
    void FrameLocal()
    {
        int x = gpuSim != null ? gpuSim.FieldMaxX : 128;
        int y = gpuSim != null ? gpuSim.FieldMaxY : 128;
        _cam.orthographic = true;
        _cam.orthographicSize = Mathf.Max(x, y) * 0.58f;
        transform.position = new Vector3(x * 0.5f, y * 0.5f, transform.position.z);
    }

    float ClampZoom(float size)
    {
        int x = gpuSim != null ? gpuSim.FieldMaxX : 128;
        int y = gpuSim != null ? gpuSim.FieldMaxY : 128;
        float maxZoomOut = Mathf.Max(x, y) * 0.58f * 2f; // можно отдалиться в 2 раза за пределы поля
        return Mathf.Clamp(size, minZoomSize, maxZoomOut);
    }

    Vector3 ClampPosition(Vector3 p, float zoomSize)
    {
        int x = gpuSim != null ? gpuSim.FieldMaxX : 128;
        int y = gpuSim != null ? gpuSim.FieldMaxY : 128;

        // Видимая половина области при ортокамере зависит от aspect.
        float halfH = zoomSize;
        float halfW = zoomSize * _cam.aspect;

        float minX = halfW - edgePadding;
        float maxX = x - halfW + edgePadding;
        float minY = halfH - edgePadding;
        float maxY = y - halfH + edgePadding;

        // Если поле меньше экрана — центрируем, не давая камере уйти.
        if (minX > maxX) { float c = x * 0.5f; p.x = c; }
        else p.x = Mathf.Clamp(p.x, minX, maxX);

        if (minY > maxY) { float c = y * 0.5f; p.y = c; }
        else p.y = Mathf.Clamp(p.y, minY, maxY);

        return p;
    }
}