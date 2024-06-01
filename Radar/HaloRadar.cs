using EFT;
using System.Collections;
using System.Collections.Generic;
using Comfort.Common;
using UnityEngine;
using UnityEngine.UI;
using EFT.Interactive;
using System.Linq;
using BepInEx.Configuration;

namespace Radar
{
    public class HaloRadar : MonoBehaviour
    {
        private bool debugInfo = false;

        private GameWorld _gameWorld = null!;
        private Player _player = null!;

        public static RectTransform RadarBorderTransform { get; private set; } = null!;
        public static RectTransform RadarBaseTransform { get; private set; } = null!;
        public static GameObject RadarBase { get; private set; } = null!;

        private RectTransform RadarPulseTransform = null!;
        
        private Coroutine? _pulseCoroutine;
        private float _radarPulseInterval = 1f;
        
        private Vector3 _radarScaleStart;

        public static float RadarLastUpdateTime = 0;

        private readonly Dictionary<string, BlipPlayer> _enemyList = new Dictionary<string, BlipPlayer>();

        private readonly List<BlipLoot> _lootCustomObject = new List<BlipLoot>();
        private Quadtree? _lootTree = null;
        private List<BlipLoot>? _activeLootOnRadar = null;
        private List<BlipLoot> _lootToHide = new List<BlipLoot>();

        // FPS Camera (this.transform.parent) -> RadarHUD (this.transform) -> RadarBaseTransform (transform.Find("Radar").transform) -> RadarBorderTransform
        private void Awake()
        {
            if (debugInfo)
                Debug.LogError("# Awake");

            if (!Singleton<GameWorld>.Instantiated)
            {
                Radar.Log.LogWarning("GameWorld singleton not found.");
                Destroy(gameObject);
                return;
            }
            
            _gameWorld = Singleton<GameWorld>.Instance;
            if (_gameWorld.MainPlayer == null)
            {
                Radar.Log.LogWarning("MainPlayer is null.");
                Destroy(gameObject);
                return;
            }
            
            _player = _gameWorld.MainPlayer;

            RadarBaseTransform = (transform.Find("Radar") as RectTransform)!;
            RadarBase = RadarBaseTransform.gameObject;
            _radarScaleStart = RadarBaseTransform.localScale;

            RadarBorderTransform = (transform.Find("Radar/RadarBorder") as RectTransform)!;
            RadarBorderTransform.SetAsLastSibling();
            RadarBorderTransform.GetComponent<Image>().color = Radar.backgroundColor.Value;
            
            RadarPulseTransform = (transform.Find("Radar/RadarPulse") as RectTransform)!;
            RadarPulseTransform.GetComponent<Image>().color = Radar.backgroundColor.Value;
            transform.Find("Radar/RadarBackground").GetComponent<Image>().color = Radar.backgroundColor.Value;

            //Debug.LogError($"& HUD: {this.transform.position} {this.transform.localPosition} {this.transform.rotation} {this.transform.localRotation} {this.transform.localScale}");
            //Debug.LogError($"& RBT: {RadarBaseTransform.position} {RadarBaseTransform.localPosition} {RadarBaseTransform.rotation} {RadarBaseTransform.localRotation} {RadarBaseTransform.localScale}");
            //Debug.LogError($"& BOD: {RadarBorderTransform.position} {RadarBorderTransform.localPosition} {RadarBorderTransform.rotation} {RadarBorderTransform.localRotation} {RadarBorderTransform.localScale}");

            Radar.Log.LogInfo("Radar loaded");
        }
        private void InitRadar()
        {
            if (Radar.radarEnableCompassConfig.Value)
                InitCompassRadar();
            else
                InitNormalRadar();
        }

        private bool compassOn = false;
        private GameObject? compassGlass;

        private void InitNormalRadar()
        {
            if (debugInfo)
                Debug.LogError("# InitNormalRadar");
            compassGlass = null;
            Canvas radarCanvas = GetComponentInChildren<Canvas>();
            RadarBase.SetActive(true);
            if (radarCanvas != null)
            {
                radarCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                radarCanvas.worldCamera = null;
            }
            transform.SetParent(GameObject.Find("FPS Camera").transform);
            transform.localScale = Vector3.one * 1.3333333f; // 1.333f is the default localScale for transform
            transform.rotation = Quaternion.identity;

            RadarBaseTransform.position = new Vector2(Radar.radarOffsetXConfig.Value, Radar.radarOffsetYConfig.Value);
            RadarBaseTransform.rotation = Quaternion.identity;
            RadarBaseTransform.localScale = _radarScaleStart * Radar.radarSizeConfig.Value;
            RadarBorderTransform.rotation = Quaternion.identity;

            //Debug.LogError($"^ {transform.position} {transform.localPosition} {transform.rotation} {transform.localRotation} {transform.localScale}");
            //Debug.LogError($"^ {RadarBaseTransform.position} {RadarBaseTransform.localPosition} {RadarBaseTransform.rotation} {RadarBaseTransform.localRotation} {RadarBaseTransform.localScale}");
        }

        public void SetCompassParent(bool enable)
        {
            if (enable && compassGlass != null)
                transform.parent = compassGlass.transform;
            else
                transform.parent = GameObject.Find("FPS Camera").transform;
        }

        private void InitCompassRadar()
        {
            if (debugInfo)
                Debug.LogError("# InitCompassRadar");
            // Ensure the Canvas is set to World Space
            Canvas radarCanvas = GetComponentInChildren<Canvas>();
            if (radarCanvas != null)
            {
                radarCanvas.renderMode = RenderMode.WorldSpace;
                radarCanvas.worldCamera = Camera.main;
            }

            // Set the parent of RadarHUD
            SetCompassParent(true);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            RadarBaseTransform.localPosition = new Vector3(0, 0, 0.001f);
            RadarBaseTransform.localRotation = Quaternion.Euler(0, -180, 0);
            RadarBaseTransform.localScale = Vector3.one * 0.000123f;
            RadarBorderTransform.localRotation = Quaternion.identity;
        }
        
        private void OnEnable()
        {
            if (debugInfo)
                Debug.LogError("# OnEnable");
            InitRadar();
            Radar.Instance.Config.SettingChanged += UpdateRadarSettings;
            UpdateRadarSettings();
        }

        private void OnDisable()
        {
            if (debugInfo)
                Debug.LogError("# OnDisable");
            Radar.Instance.Config.SettingChanged -= UpdateRadarSettings;
        }

        private void ClearLoot()
        {
            if (_lootTree != null)
            {
                _lootTree.Clear();
                _lootTree = null;
            }
            if (_lootCustomObject.Count > 0)
            {
                foreach (var loot in _lootCustomObject)
                {
                    loot.DestoryLoot();
                }
                _lootCustomObject.Clear();
            }
        }

        private void UpdateRadarSettings(object? sender = null, SettingChangedEventArgs? e = null)
        {
            if (!gameObject.activeInHierarchy) return; // Don't update if the radar object is disabled

            _radarPulseInterval = Mathf.Max(1f, Radar.radarScanInterval.Value);

            if (e == null || e.ChangedSetting == Radar.radarEnablePulseConfig)
            {
                TogglePulseAnimation(Radar.radarEnablePulseConfig.Value);
            }

            if (e != null && e.ChangedSetting == Radar.backgroundColor)
            {
                RadarBorderTransform.GetComponent<Image>().color = Radar.backgroundColor.Value;
                RadarPulseTransform.GetComponent<Image>().color = Radar.backgroundColor.Value;
                transform.Find("Radar/RadarBackground").GetComponent<Image>().color = Radar.backgroundColor.Value;
            }

            if (e != null && e.ChangedSetting == Radar.radarEnableCompassConfig)
            {
                InitRadar();
            }

            if (!Radar.radarEnableCompassConfig.Value)
            {
                if (e != null && !Radar.radarEnableCompassConfig.Value && (e.ChangedSetting == Radar.radarOffsetXConfig || e.ChangedSetting == Radar.radarOffsetYConfig))
                {
                    RadarBaseTransform.position = new Vector2(Radar.radarOffsetXConfig.Value, Radar.radarOffsetYConfig.Value);
                }

                if (e != null && e.ChangedSetting == Radar.radarSizeConfig)
                {
                    RadarBaseTransform.localScale = _radarScaleStart * Radar.radarSizeConfig.Value;
                }
            }

            if (e == null || e.ChangedSetting == Radar.radarEnableLootConfig || e.ChangedSetting == Radar.radarLootThreshold)
            {
                if (Radar.radarEnableLootConfig.Value)
                {
                    ClearLoot();
                    // Init loot items
                    var allLoot = _gameWorld.LootItems;
                    float xMin = 99999, xMax = -99999, yMin = 99999, yMax = -99999;
                    foreach (LootItem loot in allLoot.GetValuesEnumerator())
                    {
                        AddLoot(loot);
                        Vector2 loc = new Vector2(loot.TrackableTransform.position.x, loot.TrackableTransform.position.z);
                        if (loc.x < xMin)
                            xMin = loc.x;
                        if (loc.x > xMax)
                            xMax = loc.x;
                        if (loc.y < yMin)
                            yMin = loc.y;
                        if (loc.y > yMax)
                            yMax = loc.y;
                    }
                    _lootTree = new Quadtree(Rect.MinMaxRect(xMin * 1.1f, yMin * 1.1f, xMax * 1.1f, yMax * 1.1f));
                    foreach (BlipLoot loot in _lootCustomObject)
                    {
                        _lootTree.Insert(loot);
                    }
                }
                else
                {
                    ClearLoot();
                }
            }
        }

        public void AddLoot(LootItem item, bool lazyUpdate = false, int key = 0)
        {
            var price = ItemExtensions.GetBestPrice(item.Item);
            
            if (Radar.radarLootPerSlotConfig.Value)
            {
                price /= item.Item.CalculateCellSize().X * item.Item.CalculateCellSize().Y;
            }

            if (price > Radar.radarLootThreshold.Value)
            {
                var blip = new BlipLoot(item, lazyUpdate, key);
                _lootCustomObject.Add(blip);
                _lootTree?.Insert(blip);
            }
        }

        public void UpdateFireTime(string id)
        {
            if (_enemyList.ContainsKey(id))
            {
                _enemyList[id].UpdateLastFireTime(Time.time);
            }
        }

        public void RemoveLoot(int key)
        {
            Vector2 point = Vector2.zero;
            LootItem item = _gameWorld.LootItems.GetByKey(key);

            foreach (var loot in _lootCustomObject)
            {
                if (loot._key == key || loot._item == item)
                {
                    point.x = loot.targetPosition.x;
                    point.y = loot.targetPosition.z;
                    loot.DestoryLoot();
                    _lootCustomObject.Remove(loot);
                    break;
                }
            }
            _lootTree?.Remove(point, item);
        }

        private void TogglePulseAnimation(bool enable)
        {
            if (enable)
            {
                // always create a new coroutine
                if (_pulseCoroutine != null)
                {
                    StopCoroutine(_pulseCoroutine);
                }

                _pulseCoroutine = StartCoroutine(PulseCoroutine());
            }
            else if (_pulseCoroutine != null && !enable)
            {
                StopCoroutine(_pulseCoroutine);
                _pulseCoroutine = null;
            }

            RadarPulseTransform.gameObject.SetActive(enable);
        }

        private void Update()
        {
            if (_player == null) return;
            if (!Radar.radarEnableCompassConfig.Value)
            {
                RadarBorderTransform.eulerAngles = new Vector3(0, 0, transform.parent.transform.eulerAngles.y);
            }

            UpdateLoot();
            UpdateRadar(UpdateActivePlayer() != -1);

            if (Radar.radarEnableCompassConfig.Value)
            {
                Player.ItemHandsController? handsController = _player.HandsController as Player.ItemHandsController;
                var compassInHand = handsController != null && handsController.CurrentCompassState;
                if (compassInHand && !compassOn)
                {
                    compassOn = true;
                    RadarBase.SetActive(true);
                    SetCompassParent(true);
                }
                if (!compassInHand && compassOn)
                {
                    compassOn = false;
                    RadarBase.SetActive(false);
                    SetCompassParent(false);
                }

                if (compassOn && compassGlass == null)
                {
                    compassGlass = GameObject.Find("compas_glass_LOD0");
                    if (compassGlass != null && transform.parent != compassGlass.transform)
                    {
                        transform.parent = compassGlass.transform;
                        transform.localPosition = Vector3.zero;
                        transform.localRotation = Quaternion.identity;
                    }
                }
            }
        }

        private IEnumerator PulseCoroutine()
        {
            while (true)
            {
                // Rotate from 360 to 0 over the animation duration
                float t = 0f;
                while (t < 1.0f)
                {
                    t += Time.deltaTime / _radarPulseInterval;
                    float angle = Mathf.Lerp(0f, 1f, 1 - t) * 360;

                    // Apply the scale to all axes
                    RadarPulseTransform.localEulerAngles = new Vector3(0, 0, angle);
                    yield return null;
                }
                // Pause for the specified duration
                // yield return new WaitForSeconds(interval);
            }
        }

        private long UpdateActivePlayer()
        {
            float interval = Radar.radarScanInterval.Value;
            if (Radar.radarEnableFireModeConfig.Value)
                interval = 0.1f;
                
            if (Time.time - RadarLastUpdateTime < interval)
            {
                return -1;
            }
            else
            {
                RadarLastUpdateTime = Time.time;
            }
            IEnumerable<Player> allPlayers = _gameWorld.AllPlayersEverExisted;

            if (allPlayers.Count() == _enemyList.Count + 1)
            {
                return -2;
            }

            foreach (Player enemyPlayer in allPlayers)
            {
                if (enemyPlayer == null || enemyPlayer == _player)
                {
                    continue;
                }
                if (!_enemyList.ContainsKey(enemyPlayer.ProfileId))
                {
                    _enemyList.Add(enemyPlayer.ProfileId, new BlipPlayer(enemyPlayer));
                }
            }
            return 0;
        }

        private void UpdateLoot()
        {
            if (Time.time - RadarLastUpdateTime < Radar.radarScanInterval.Value)
            {
                return;
            }
            Vector2 center = new Vector2(_player.Transform.position.x, _player.Transform.position.z);
            var latestActiveLootOnRadar = _lootTree?.QueryRange(center, Radar.radarOuterRangeConfig.Value);
            _lootToHide.Clear();
            if (_activeLootOnRadar != null)
            {
                foreach (var old in _activeLootOnRadar)
                {
                    if (latestActiveLootOnRadar == null || !latestActiveLootOnRadar.Contains(old))
                    {
                        _lootToHide.Add(old);
                    }
                }
            }

            _activeLootOnRadar?.Clear();
            _activeLootOnRadar = latestActiveLootOnRadar;
        }

        private void UpdateRadar(bool positionUpdate = true)
        {
            Target.setPlayerPosition(_player.Transform.position);
            Target.setRadarRange(Radar.radarInnerRangeConfig.Value, Radar.radarOuterRangeConfig.Value);
            foreach (var obj in _enemyList)
            {
                obj.Value.Update(positionUpdate);
            }

            foreach (var obj in _lootToHide)
            {
                obj.Update(false);
            }

            if (_activeLootOnRadar != null)
            {
                foreach (var obj in _activeLootOnRadar)
                {
                    obj.Update(true);
                }
            }
        }
    }
}