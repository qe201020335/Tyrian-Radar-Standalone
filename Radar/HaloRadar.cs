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

namespace Radar
{
    public class HaloRadar : MonoBehaviour
    {
        private GameWorld _gameWorld = null!;
        private Player _player = null!;

        public static RectTransform RadarHudBlipBasePosition { get; private set; } = null!;
        public static RectTransform RadarHudBasePosition { get; private set; } = null!;
        
        private RectTransform _radarHudPulse = null!;
        
        private Coroutine? _pulseCoroutine;
        private float _radarPulseInterval = 1f;
        
        private Vector3 _radarScaleStart;
        private float _radarPositionYStart = 0f;
        private float _radarPositionXStart = 0f;

        public static float RadarLastUpdateTime = 0;

        private readonly Dictionary<int, BlipPlayer> _enemyList = new Dictionary<int, BlipPlayer>();

        private readonly List<BlipLoot> _lootCustomObject = new List<BlipLoot>();
        private Quadtree? _lootTree = null;
        private List<BlipLoot>? _activeLootOnRadar = null;
        private List<BlipLoot> _lootToHide = new List<BlipLoot>();

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

            RadarHudBasePosition = (transform.Find("Radar") as RectTransform)!;
            RadarHudBlipBasePosition = (transform.Find("Radar/RadarBorder") as RectTransform)!;
            RadarHudBlipBasePosition.SetAsLastSibling();
            _radarHudPulse = (transform.Find("Radar/RadarPulse") as RectTransform)!;
            _radarScaleStart = RadarHudBasePosition.localScale;
            _radarPositionYStart = RadarHudBasePosition.position.y;
            _radarPositionXStart = RadarHudBasePosition.position.x;
            //RadarHudBasePosition.position = new Vector2(_radarPositionXStart + Radar.radarOffsetXConfig.Value, _radarPositionYStart + Radar.radarOffsetYConfig.Value);
            RadarHudBasePosition.localScale = new Vector2(_radarScaleStart.x * Radar.radarSizeConfig.Value, _radarScaleStart.y * Radar.radarSizeConfig.Value);
            RadarHudBasePosition.localPosition = new Vector3(0, 0, 0);


            RadarHudBlipBasePosition.GetComponent<Image>().color = Radar.backgroundColor.Value;
            _radarHudPulse.GetComponent<Image>().color = Radar.backgroundColor.Value;
            transform.Find("Radar/RadarBackground").GetComponent<Image>().color = Radar.backgroundColor.Value;
            
            Radar.Log.LogInfo("Radar loaded");
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

            _radarHudPulse.gameObject.SetActive(enable);
        }

        private void Update()
        {
            //RadarHudBasePosition.position = new Vector2(_radarPositionXStart + Radar.radarOffsetXConfig.Value, _radarPositionYStart + Radar.radarOffsetYConfig.Value);
            //RadarHudBasePosition.localScale = new Vector2(_radarScaleStart.x * Radar.radarSizeConfig.Value, _radarScaleStart.y * Radar.radarSizeConfig.Value);
            //RadarHudBlipBasePosition.eulerAngles = new Vector3(0, 0, transform.parent.transform.eulerAngles.y);
            
            UpdateLoot();
            long rslt = UpdateActivePlayer();
            UpdateRadar(rslt != -1);
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
                    _radarHudPulse.localEulerAngles = new Vector3(0, 0, angle);
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