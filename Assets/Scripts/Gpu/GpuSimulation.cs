using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace GpuSim
{
    public class GpuSimulation : MonoBehaviour
    {
        [Header("Размеры")]
        [SerializeField] private int maxX = 128;
        [SerializeField] private int maxY = 128;

        [Header("Параметры симуляции")]
        [SerializeField] private int maxCommand = 12;
        [SerializeField] private int initialEnergy = 20;
        [SerializeField] private int breedCost = 30;
        [SerializeField] private int foodDecay = 1;
        [SerializeField] private int corpseFood = 23;
        [SerializeField] private int cellLifespan = 1000;
        [SerializeField] private int lightMode = 0;
        [SerializeField] private float lightLevel = 1f;
        [SerializeField] private float statBonus = 0.10f;
        [SerializeField] private int targetPopulation = 4000;
        [SerializeField] private int spawnPerFrame = 60;
        [SerializeField] private bool spawnEnabled = true;
        [SerializeField] private bool autoStart = false;

        [Header("Рендер")]
        [SerializeField] private bool draw = true;
        [SerializeField] private int viewMode = 0;       // 0=ДНК,1=энергия,2=возраст,3=действие,4=колония
        [SerializeField] private ComputeShader compute;
        [SerializeField] private Material instancedMat;

        [Header("UI (опционально)")]
        [SerializeField] private Text text;

        // Буферы
        private ComputeBuffer _grid, _energy, _energyNext, _cells, _cellsNext;
        private ComputeBuffer _dna, _genes, _intents, _claim, _freeList;
        private ComputeBuffer _infectOwner, _stats, _spawnDone, _matBuf, _colorBuf;
        private ComputeBuffer _args;

        private int maxCells;
        private int step = 0;
        private bool running = false;   // симуляция инициализирована (буферы созданы)
        private bool paused = false;    // на паузе (шаги не выполняются)
        private int stepsPerFrame = 1;  // скорость: сколько шагов за кадр
        private int kernelSim, kernelClear, kernelClaim, kernelApply, kernelInfect, kernelCompact, kernelSpawn, kernelFill;

        private Mesh quadMesh;
        // 2D-диспетчеризация обхода всех клеток: D3D11 лимит — 65535 групп на ось Dispatch.
        // При больших полях (maxX*maxY/64 > 65535) раскладываем группы по осям X и Y,
        // а плоский индекс клетки собираем в шейдере: id = dtid.x + dtid.y * flatWidth.
        private int groupX, groupY, flatWidth;

        // Периодический readback статистики
        private int[] _statsHost = new int[9];
        private int _readbackFrame;

        public bool IsRunning => running;
        public bool IsPaused => paused;
        public int Step => step;
        public int StepsPerFrame => stepsPerFrame;
        public int AliveCount => _statsHost != null ? _statsHost[0] : 0;
        public int FoodCount => _statsHost != null ? _statsHost[1] : 0;

        // Доступ к GPU-буферу статистики и его размеру для внешнего сборщика (StatsCollector).
        public ComputeBuffer StatsBuffer => _stats;
        public int StatsSlotCount => 9;

        // Границы поля для камеры (действительны после StartSim; до него — значения по умолчанию).
        public int FieldMaxX => maxX;
        public int FieldMaxY => maxY;

        // Настройка перед StartSim (вызывается Controller). Старая сигнатура для обратной совместимости.
        public void Configure(int x, int y, int cmd, int energy, int breed, float bonus, bool doDraw)
        {
            maxX = Mathf.Clamp(x, 8, 2048);
            maxY = Mathf.Clamp(y, 8, 2048);
            maxCommand = Mathf.Clamp(cmd, 1, 64);
            initialEnergy = Mathf.Clamp(energy, 1, 255);
            breedCost = Mathf.Clamp(breed, 1, 255);
            statBonus = bonus;
            draw = doDraw;
        }

        // Полная настройка из SimSettings.
        public void Configure(SimSettings s)
        {
            if (s == null) return;
            maxX = Mathf.Clamp(s.sizeX, 8, 2048);
            maxY = Mathf.Clamp(s.sizeY, 8, 2048);
            maxCommand = Mathf.Clamp(s.maxCommand, 1, 64);
            initialEnergy = Mathf.Clamp(s.initialEnergy, 1, 255);
            breedCost = Mathf.Clamp(s.breedCost, 1, 255);
            cellLifespan = Mathf.Clamp(s.cellLifespan, 0, 100000);
            lightMode = Mathf.Clamp(s.lightMode, 0, 2);
            lightLevel = Mathf.Clamp(s.lightLevel, 0f, 2f);
            foodDecay = Mathf.Clamp(s.foodDecay, 0, 20);
            corpseFood = Mathf.Clamp(s.corpseFood, 0, 255);
            statBonus = Mathf.Clamp(s.statBonus, 0f, 0.5f);
            targetPopulation = Mathf.Clamp(s.targetPopulation, 0, 1000000);
            spawnPerFrame = Mathf.Clamp(s.spawnPerFrame, 0, 10000);
            spawnEnabled = s.spawnEnabled;
            autoStart = s.autoStart;
            draw = s.draw;
        }

        // Применить изменённые в рантайме параметры (без пересоздания буферов).
        // Размеры поля меняются только через Configure + StartSim (пересоздание буферов).
        public void ApplyRuntimeParams()
        {
            if (compute == null) return;
            compute.SetInt("_MaxCommand", maxCommand);
            compute.SetInt("_InitialEnergy", initialEnergy);
            compute.SetInt("_BreedCost", breedCost);
            compute.SetInt("_FoodDecay", foodDecay);
            compute.SetInt("_CorpseFood", corpseFood);
            compute.SetInt("_CellLifespan", cellLifespan);
            compute.SetInt("_LightMode", lightMode);
            compute.SetFloat("_LightLevel", lightLevel);
            compute.SetFloat("_StatBonus", statBonus);
            ApplyViewMode();
        }

        void Start()
        {
            if (compute == null) compute = Resources.Load<ComputeShader>("GpuSim");
            if (instancedMat == null && Shader.Find("GpuSim/InstancedQuad") != null)
                instancedMat = new Material(Shader.Find("GpuSim/InstancedQuad"));
            if (autoStart && compute != null) StartSim();
        }

        void OnDestroy()
        {
            Release(new[] { _grid, _energy, _energyNext, _cells, _cellsNext, _dna, _genes,
                _intents, _claim, _freeList, _infectOwner, _stats, _spawnDone,
                _matBuf, _colorBuf, _args });
        }

        public void StartSim()
        {
            if (running) StopSim();
            if (compute == null) compute = Resources.Load<ComputeShader>("GpuSim");
            if (instancedMat == null && Shader.Find("GpuSim/InstancedQuad") != null)
                instancedMat = new Material(Shader.Find("GpuSim/InstancedQuad"));

            maxCells = maxX * maxY;
            // Группы потока для обхода всех клеток (64 потока на группу).
            // D3D11 допускает не более 65535 групп на ось — при переполнении переходим в 2D.
            int totalGroups = Mathf.CeilToInt(maxCells / 64f);
            if (totalGroups <= 65535) { groupX = totalGroups; groupY = 1; }
            else { groupY = Mathf.CeilToInt(totalGroups / 65535f); groupX = 65535; }
            flatWidth = groupX * 64;
            CacheKernels();
            CreateBuffers();
            InitState();
            running = true;
            paused = false;
            FrameCamera();
        }

        public void StopSim()
        {
            running = false;
            paused = false;
            Release(new[] { _grid, _energy, _energyNext, _cells, _cellsNext, _dna, _genes,
                _intents, _claim, _freeList, _infectOwner, _stats, _spawnDone,
                _matBuf, _colorBuf, _args });
            _grid = _energy = _energyNext = _cells = _cellsNext = _dna = _genes =
            _intents = _claim = _freeList = _infectOwner = _stats = _spawnDone =
            _matBuf = _colorBuf = _args = null;
        }

        // Пауза/возобновление.
        public void TogglePause() => paused = !paused;
        public void SetPaused(bool p) => paused = p;

        // Скорость: сколько шагов симуляции за один кадр (1..N).
        public void SetSpeed(int spf) => stepsPerFrame = Mathf.Clamp(spf, 1, 64);

        // Пошаговый режим: выполнить ровно один шаг, оставаясь на паузе.
        public void StepOnce()
        {
            if (!running || compute == null) return;
            DoStep();
        }

        public void ToggleDraw() => draw = !draw;

        // Режим отображения: 0=ДНК,1=энергия,2=возраст,3=действие,4=колония.
        public int ViewMode => viewMode;
        public void SetViewMode(int mode) { viewMode = Mathf.Clamp(mode, 0, 4); ApplyViewMode(); }
        public void ToggleViewMode() { viewMode = (viewMode + 1) % 5; ApplyViewMode(); }

        void ApplyViewMode()
        {
            if (compute == null) return;
            compute.SetInt("_ViewMode", viewMode);
            // Обратная совместимость со старой логикой шейдера: _EnergyView=1 только в режиме энергии.
            compute.SetInt("_EnergyView", viewMode == 1 ? 1 : 0);
        }

        void Update()
        {
            if (!running || paused) return;

            for (int i = 0; i < stepsPerFrame; i++) DoStep();

            // Отрисовка — один раз за кадр, после всех шагов.
            if (draw)
            {
                CellDispatch(kernelFill);
                if (instancedMat != null && quadMesh != null)
                    Graphics.DrawMeshInstancedIndirect(quadMesh, 0, instancedMat,
                        new Bounds(new Vector3(maxX * 0.5f, maxY * 0.5f, 0),
                                   new Vector3(maxX + 2, maxY + 2, 100)), _args);
            }

            if (text != null) text.text = $"step {step}";

            // статистика каждые 30 кадров
            if (Time.frameCount - _readbackFrame >= 30)
            {
                _readbackFrame = Time.frameCount;
                AsyncGPUReadback.Request(_stats, r =>
                {
                    if (r.done && !r.hasError) _statsHost = r.GetData<int>().ToArray();
                });
            }
        }

        // Один шаг симуляции (без отрисовки). Может вызываться из Update (цикл скорости)
        // и из StepOnce (пошаговый режим при паузе).
        void DoStep()
        {
            uint seed = (uint)((step + 1) * 2654435761u) ^ 0x9e3779b9u;
            compute.SetInt("_RandSeed", (int)seed);
            compute.SetInt("_Step", step);
            compute.SetInt("_ViewMode", viewMode);
            compute.SetInt("_EnergyView", viewMode == 1 ? 1 : 0);

            CellDispatch(kernelClear);
            CellDispatch(kernelSim);
            Commit();
            CellDispatch(kernelClaim);
            CellDispatch(kernelApply);
            CellDispatch(kernelInfect);
            CellDispatch(kernelCompact);

            if (spawnEnabled)
            {
                compute.SetInt("_SpawnCount", spawnPerFrame);
                Dispatch(kernelSpawn, Mathf.CeilToInt(spawnPerFrame / 64f) + 1);
            }

            step++;
        }

        // Отрисовка в render-phase колбэке (Built-in RP): вызывается камерой при рендере.
        void OnRenderObject()
        {
            if (!running || !draw || instancedMat == null || quadMesh == null) return;
            Graphics.DrawMeshInstancedIndirect(quadMesh, 0, instancedMat,
                new Bounds(new Vector3(maxX * 0.5f, maxY * 0.5f, 0),
                           new Vector3(maxX + 2, maxY + 2, 100)), _args);
        }

        // Авто-кадрирование главной камеры под размер поля.
        public void FrameCamera()
        {
            var cam = Camera.main;
            if (cam == null) cam = FindFirstObjectByType<Camera>();
            if (cam == null) return;
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(maxX, maxY) * 0.58f;
            cam.transform.position = new Vector3(maxX * 0.5f, maxY * 0.5f, -10f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        }

        // ---------- внутренние ----------

        void Dispatch(int k, int groups)
        {
            if (groups <= 0) groups = 1;
            compute.Dispatch(k, groups, 1, 1);
        }

        // Диспетчеризация ядра, обходящего все клетки поля, в 2D-сетке групп
        // (обходит лимит 65535 групп на ось). Плоский id собирается в шейдере.
        void CellDispatch(int k)
        {
            compute.Dispatch(k, groupX, groupY, 1);
        }

        void Commit()
        {
            // _CellsNext/_EnergyNext -> _Cells/_Energy (копирование через переименование буферов)
            Swap(ref _cells, ref _cellsNext);
            Swap(ref _energy, ref _energyNext);
            // перепривязать буферы, т.к. поменялись местами
            BindBuffers();
        }

        static void Swap<T>(ref T a, ref T b) { T t = a; a = b; b = t; }

        void CacheKernels()
        {
            kernelSim = compute.FindKernel("SimStep");
            kernelClear = compute.FindKernel("Clear");
            kernelClaim = compute.FindKernel("ResolveClaim");
            kernelApply = compute.FindKernel("ResolveApply");
            kernelInfect = compute.FindKernel("ApplyInfection");
            kernelCompact = compute.FindKernel("Compact");
            kernelSpawn = compute.FindKernel("Spawn");
            kernelFill = compute.FindKernel("FillInstances");
        }

        void CreateBuffers()
        {
            int cells = maxCells;
            int slots = maxX * maxY;
            _grid = new ComputeBuffer(slots, sizeof(int));
            _energy = new ComputeBuffer(cells, sizeof(int));
            _energyNext = new ComputeBuffer(cells, sizeof(int));
            _cells = new ComputeBuffer(cells, System.Runtime.InteropServices.Marshal.SizeOf(typeof(CellState)));
            _cellsNext = new ComputeBuffer(cells, System.Runtime.InteropServices.Marshal.SizeOf(typeof(CellState)));
            _dna = new ComputeBuffer(cells * 20, sizeof(uint));
            _genes = new ComputeBuffer(cells, sizeof(uint));
            _intents = new ComputeBuffer(cells, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Intent)));
            _claim = new ComputeBuffer(slots, sizeof(int));
            _freeList = new ComputeBuffer(cells + 1, sizeof(int)); // [0]=счётчик, [1..]=индексы
            _infectOwner = new ComputeBuffer(cells, sizeof(int));
            _stats = new ComputeBuffer(9, sizeof(int));
            _spawnDone = new ComputeBuffer(1, sizeof(int));
            _matBuf = new ComputeBuffer(cells, 64); // float4x4
            _colorBuf = new ComputeBuffer(cells, 16); // float4
            _args = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);

            // args для DrawMeshInstancedIndirect: indexCount, instanceCount, startIndex, baseVertex, startInstance
            _args.SetData(new uint[] { 6, (uint)cells, 0, 0, 0 });

            quadMesh = BuildQuad();
            BindBuffers();
        }

        // Привязка конкретного буфера к набору имён (RW и/или RO-алиас) в одном ядре.
        void Bind(int k, params (string name, ComputeBuffer buf)[] pairs)
        {
            foreach (var p in pairs) compute.SetBuffer(k, p.name, p.buf);
        }

        void BindKernel(int k, string which)
        {
            switch (which)
            {
                case "SimStep":
                    Bind(k, ("_CellsNext", _cellsNext), ("_EnergyNext", _energyNext), ("_Intents", _intents),
                            ("_GridRO", _grid), ("_EnergyRO", _energy), ("_CellsRO", _cells),
                            ("_DnaProgRO", _dna), ("_GenesRO", _genes));
                    break;
                case "Clear":
                    Bind(k, ("_Claim", _claim), ("_InfectOwner", _infectOwner),
                            ("_Stats", _stats), ("_SpawnDone", _spawnDone));
                    break;
                case "ResolveClaim":
                    Bind(k, ("_Claim", _claim), ("_IntentsRO", _intents));
                    break;
                case "ResolveApply":
                    Bind(k, ("_IntentsRO", _intents), ("_ClaimRO", _claim),
                            ("_Cells", _cells), ("_Energy", _energy), ("_Grid", _grid),
                            ("_DnaProg", _dna), ("_Genes", _genes),
                            ("_FreeList", _freeList), ("_InfectOwner", _infectOwner),
                            ("_Stats", _stats));
                    break;
                case "ApplyInfection":
                    Bind(k, ("_DnaProg", _dna), ("_InfectOwnerRO", _infectOwner), ("_Cells", _cells));
                    break;
                case "Compact":
                    Bind(k, ("_Cells", _cells), ("_Energy", _energy), ("_Grid", _grid),
                            ("_FreeList", _freeList), ("_Stats", _stats));
                    break;
                case "Spawn":
                    Bind(k, ("_Grid", _grid), ("_Cells", _cells), ("_Energy", _energy),
                            ("_DnaProg", _dna), ("_Genes", _genes),
                            ("_FreeList", _freeList), ("_SpawnDone", _spawnDone));
                    break;
                case "FillInstances":
                    Bind(k, ("_CellsRO", _cells), ("_EnergyRO", _energy), ("_DnaProgRO", _dna),
                            ("_IntentsRO", _intents), ("_MatBuf", _matBuf), ("_ColorBuf", _colorBuf));
                    break;
            }
        }

        void BindBuffers()
        {
            string[] names = { "SimStep", "Clear", "ResolveClaim", "ResolveApply",
                                "ApplyInfection", "Compact", "Spawn", "FillInstances" };
            for (int i = 0; i < 8; i++)
            {
                int k = compute.FindKernel(names[i]);
                BindKernel(k, names[i]);
            }
            compute.SetInt("_MaxX", maxX);
            compute.SetInt("_MaxY", maxY);
            compute.SetInt("_MaxCells", maxCells);
            compute.SetInt("_DispatchW", flatWidth); // шаг плоской развёртки 2D-диспетчера
            compute.SetInt("_MaxCommand", maxCommand);
            compute.SetInt("_InitialEnergy", initialEnergy);
            compute.SetInt("_BreedCost", breedCost);
            compute.SetInt("_FoodDecay", foodDecay);
            compute.SetInt("_CorpseFood", corpseFood);
            compute.SetInt("_CellLifespan", cellLifespan);
            compute.SetInt("_LightMode", lightMode);
            compute.SetFloat("_LightLevel", lightLevel);
            compute.SetInt("_ViewMode", viewMode);
            compute.SetInt("_EnergyView", viewMode == 1 ? 1 : 0);
            compute.SetFloat("_StatBonus", statBonus);

            if (instancedMat != null)
            {
                instancedMat.SetBuffer("_MatBuf", _matBuf);
                instancedMat.SetBuffer("_ColorBuf", _colorBuf);
            }
        }

        void InitState()
        {
            int slots = maxX * maxY;
            int[] grid = new int[slots];
            for (int i = 0; i < slots; i++) grid[i] = -1;
            _grid.SetData(grid);

            CellState[] cs = new CellState[maxCells];
            for (int i = 0; i < maxCells; i++) { cs[i].flags = SimFlags.FREE | SimFlags.INPOOL; cs[i].id = i; }
            _cells.SetData(cs);
            _cellsNext.SetData(cs);

            int[] en = new int[maxCells];
            _energy.SetData(en); _energyNext.SetData(en);

            // Free list: _FreeList[0]=count (=MaxCells, все свободны), [1..MaxCells]=0..MaxCells-1.
            // Стартовые клетки помечены FREE|INPOOL (уже в пуле).
            int[] free = new int[maxCells + 1];
            free[0] = maxCells;
            for (int i = 0; i < maxCells; i++) free[i + 1] = i;
            _freeList.SetData(free);

            int[] neg = new int[maxCells];
            for (int i = 0; i < maxCells; i++) neg[i] = -1;
            _infectOwner.SetData(neg);
            _claim.SetData(grid);

            _stats.SetData(new int[9]);
            _spawnDone.SetData(new int[1]);

            // случайные ДНК/гены для всех слотов (Spawn всё равно перезапишет создаваемые)
            uint[] dna = new uint[maxCells * 20];
            uint seed = 0x1234u;
            for (int i = 0; i < dna.Length; i++) { seed = seed * 747796405u + 2891336453u; dna[i] = seed; }
            _dna.SetData(dna);
            uint[] gn = new uint[maxCells];
            for (int i = 0; i < gn.Length; i++) { seed = seed * 747796405u + 2891336453u; gn[i] = seed; }
            _genes.SetData(gn);

            // первичный спавн
            compute.SetInt("_RandSeed", (int)0xabcdefu);
            compute.SetInt("_SpawnCount", targetPopulation);
            Dispatch(kernelSpawn, Mathf.CeilToInt(targetPopulation / 64f) + 1);
        }

        static void Release(IEnumerable<ComputeBuffer> bufs)
        {
            foreach (var b in bufs) if (b != null) b.Release();
        }

        static Mesh BuildQuad()
        {
            Mesh m = new Mesh { name = "GpuSimQuad" };
            Vector3[] v = new Vector3[] {
                new Vector3(-0.5f,-0.5f,0), new Vector3(0.5f,-0.5f,0),
                new Vector3(0.5f,0.5f,0), new Vector3(-0.5f,0.5f,0) };
            int[] t = new int[] { 0, 1, 2, 0, 2, 3 };
            m.vertices = v; m.triangles = t; m.RecalculateNormals();
            return m;
        }
    }
}