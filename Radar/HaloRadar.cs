using EFT;
using System.Collections;
using System.Collections.Generic;
using Comfort.Common;
using UnityEngine;
using UnityEngine.UI;
using EFT.Interactive;
using System.Linq;
using BepInEx.Configuration;
using Aki.Reflection.Utils;
using System.Data;

namespace Radar
{
    public class HaloRadar : MonoBehaviour
    {
        private GameWorld _gameWorld = null!;
        private Player _player = null!;

        public static RectTransform RadarBorderTransform { get; private set; } = null!;
        public static RectTransform RadarBaseTransform { get; private set; } = null!;
        
        private RectTransform RadarPulseTransform = null!;
        
        private Coroutine? _pulseCoroutine;
        private float _radarPulseInterval = 1f;
        
        private Vector3 _radarScaleStart;

        public static float RadarLastUpdateTime = 0;

        private readonly Dictionary<int, BlipPlayer> _enemyList = new Dictionary<int, BlipPlayer>();

        private readonly List<BlipLoot> _lootCustomObject = new List<BlipLoot>();
        private Quadtree? _lootTree = null;
        private List<BlipLoot>? _activeLootOnRadar = null;
        private List<BlipLoot> _lootToHide = new List<BlipLoot>();

        // FPS Camera -> RadarHUD -> RadarBaseTransform -> RadarBorderTransform
        private void Awake()
        {
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
            _radarScaleStart = RadarBaseTransform.localScale;
            RadarBaseTransform.localScale = RadarBaseTransform.localScale * Radar.radarSizeConfig.Value;

            RadarBorderTransform = (transform.Find("Radar/RadarBorder") as RectTransform)!;
            RadarBorderTransform.SetAsLastSibling();
            RadarBorderTransform.GetComponent<Image>().color = Radar.backgroundColor.Value;
            
            RadarPulseTransform = (transform.Find("Radar/RadarPulse") as RectTransform)!;
            RadarPulseTransform.GetComponent<Image>().color = Radar.backgroundColor.Value;
            transform.Find("Radar/RadarBackground").GetComponent<Image>().color = Radar.backgroundColor.Value;

            Debug.LogError($"$ FPS: {transform.parent} {transform.parent.position} {transform.parent.localPosition}");
            Debug.LogError($"$ HUD: {transform} {transform.position} {transform.localPosition}");
            Debug.LogError($"$ RBT: {RadarBaseTransform} {RadarBaseTransform.position} {RadarBaseTransform.localPosition} {RadarBaseTransform.parent}");
            
            Radar.Log.LogInfo("Radar loaded");

            if (Radar.radarEnableCompassConfig.Value)
            {
                InitCompassRadar();
            }
            else
            {
                InitNormalRadar();
            }
        }

        private RenderTexture radarRenderTexture = new RenderTexture(256, 256, 16);
        private GameObject radarCameraObject = new GameObject("RadarCamera");
        private Camera? radarCamera;
        private bool compassOn = false;
        private GameObject? compassGlass;

        private void LateUpdate()
        {
            if (compassOn && compassGlass != null)
            {
                Vector3 compassPosition = compassGlass.transform.position;
                radarCamera.transform.position = compassPosition;
                radarCamera.transform.rotation = compassGlass.transform.rotation;
            }
        }

        private void InitNormalRadar()
        {
            compassGlass = null;
            Canvas radarCanvas = GetComponentInChildren<Canvas>();
            if (radarCanvas != null)
            {
                radarCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                radarCanvas.worldCamera = null;
            }
            this.transform.SetParent(GameObject.Find("FPS Camera").transform, true);
            RadarBaseTransform.position = new Vector2(Radar.radarOffsetXConfig.Value, Radar.radarOffsetYConfig.Value);
            Debug.LogError($"# FPS: {transform.parent} {transform.parent.position} {transform.parent.localPosition}");
            Debug.LogError($"# HUD: {transform} {transform.position} {transform.localPosition}");
            Debug.LogError($"# RBT: {RadarBaseTransform} {RadarBaseTransform.position} {RadarBaseTransform.localPosition} {RadarBaseTransform.parent}");
        }

        private void InitCompassRadar()
        {
            RadarBaseTransform.localPosition = new Vector3(0, 0, 0);

            if (radarCamera == null)
            {
                // Create a RenderTexture
                radarRenderTexture.Create();

                // Create a new camera for rendering the radar HUD
                radarCamera = radarCameraObject.AddComponent<Camera>();

                // Configure the camera
                radarCamera.targetTexture = radarRenderTexture;
                radarCamera.orthographic = true;
                radarCamera.orthographicSize = 0.1f;
                radarCamera.clearFlags = CameraClearFlags.SolidColor;
                radarCamera.backgroundColor = Color.clear;

                // Position the camera to render your radar HUD
                radarCamera.transform.position = new Vector3(0, 0, 0);
                radarCamera.transform.rotation = Quaternion.identity;
                radarCamera.transform.localScale = new Vector3(0.003f, 0.003f, 0.003f);
            }

            // Ensure the Canvas is set to World Space
            Canvas radarCanvas = GetComponentInChildren<Canvas>();
            if (radarCanvas != null)
            {
                radarCanvas.renderMode = RenderMode.WorldSpace;
                radarCanvas.worldCamera = radarCamera;
            }

            // Set the radar HUD to be rendered by the radar camera
            this.transform.SetParent(radarCamera.transform, false);

            // Adjust the size and position of the radar HUD if necessary
            //RectTransform radarHudRectTransform = GetComponent<RectTransform>();
            //if (radarHudRectTransform != null)
            //{
            //    radarHudRectTransform.localScale = new Vector2(0.1f, 0.1f);  // Adjust size as needed
            //    radarHudRectTransform.localPosition = Vector3.zero;
            //}
            Debug.LogError($"X parent transform {this.transform.parent.name} {this.transform.localPosition}");
        }
        
        private void OnEnable()
        {
            Radar.Instance.Config.SettingChanged += UpdateRadarSettings;
            UpdateRadarSettings();
        }
        
        private void OnDisable()
        {
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

            if (e != null && (e.ChangedSetting == Radar.radarOffsetXConfig || e.ChangedSetting == Radar.radarOffsetYConfig))
            {
                RadarBaseTransform.position = new Vector2(Radar.radarOffsetXConfig.Value, Radar.radarOffsetYConfig.Value);
                Debug.LogError($"Base xxx: {transform.name} {transform.position} {RadarBaseTransform.localPosition}");
                Debug.LogError($"% FPS: {transform.parent} {transform.parent.position} {transform.parent.localPosition}");
                Debug.LogError($"% HUD: {transform} {transform.position} {transform.localPosition}");
                Debug.LogError($"% RBT: {RadarBaseTransform} {RadarBaseTransform.position} {RadarBaseTransform.localPosition} {RadarBaseTransform.parent}");
            }

            if (e != null && e.ChangedSetting == Radar.radarSizeConfig)
            {
                RadarBaseTransform.localScale = new Vector2(_radarScaleStart.x * Radar.radarSizeConfig.Value, _radarScaleStart.y * Radar.radarSizeConfig.Value);
            }

            //if (e == null || e.ChangedSetting == Radar.radarEnableCompassConfig)
            //{
            //    if (Radar.radarEnableCompassConfig.Value)
            //    {
            //        InitCompassRadar();
            //    }
            //    else
            //    {
            //        InitNormalRadar();
            //    }
            //}

            if ((e == null || e.ChangedSetting == Radar.radarEnableLootConfig || e.ChangedSetting == Radar.radarLootThreshold))
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
                    //Debug.LogError($"Add {_lootCustomObject.Count} items, Min/Max x/z: {xMin} {xMax} {yMin} {yMax}");
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
            var blip = new BlipLoot(item, lazyUpdate, key);
            var price = blip._price;
            if (Radar.radarLootPerSlotConfig.Value)
            {
                price /= item.Item.CalculateCellSize().X * item.Item.CalculateCellSize().Y;
            }

            if (price > Radar.radarLootThreshold.Value)
            {
                blip.SetBlip();
                _lootCustomObject.Add(blip);
                _lootTree?.Insert(blip);
            }
            else
            {
                blip.DestoryLoot();
            }
        }

        public void UpdateFireTime(int id)
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
            if (!compassOn)
            {
                RadarBorderTransform.eulerAngles = new Vector3(0, 0, transform.parent.transform.eulerAngles.y);
            }
            
            
            UpdateLoot();
            long rslt = UpdateActivePlayer();
            UpdateRadar(rslt != -1);

            //if (Radar.radarEnableCompassConfig.Value)
            //{
            //    Player.ItemHandsController? handsController = _player.HandsController as Player.ItemHandsController;
            //    if (handsController != null && handsController.CurrentCompassState)
            //    {
            //        compassOn = true;
            //    }
            //    else
            //    {
            //        compassOn = false;
            //    }
            //    if (compassOn && compassGlass == null)
            //    {
            //        compassGlass = GameObject.Find("compas_glass_LOD0");
            //        if (compassGlass != null)
            //        {
            //            Renderer compassRenderer = compassGlass.GetComponent<Renderer>();
            //            if (compassRenderer != null)
            //            {
            //                compassRenderer.material.mainTexture = radarRenderTexture;
            //            }
            //        }
            //    }
            //}
            
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
                if (!_enemyList.ContainsKey(enemyPlayer.Id))
                {
                    var blip = new BlipPlayer(enemyPlayer);
                    blip.SetBlip();
                    _enemyList.Add(enemyPlayer.Id, blip);
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