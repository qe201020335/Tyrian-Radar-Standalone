using EFT;
using System;
using UnityEngine;

namespace Radar
{
    public class InRaidRadarManager : MonoBehaviour
    {
        static public GameObject _radarGo;

        private bool _enableSCDown = false;
        private bool _corpseSCDown = false;
        private bool _lootSCDown = false;

        private RenderTexture radarRenderTexture;
        private Camera radarCamera;
        private bool compassOn = false;
        private GameObject compassGlass;
        private bool isInit = false;

        private void Awake()
        {
            new GClass723PatchAdd().Enable();
            new GClass723PatchRemove().Enable();
            new PlayerOnMakingShotPatch().Enable();
            var playerCamera = GameObject.Find("FPS Camera");
            if (playerCamera == null)
            {
                Radar.Log.LogError("FPS Camera not found");
                Destroy(gameObject);
                return;
            }

            // move the radar camera into haloradar

            _radarGo = Instantiate(AssetBundleManager.RadarhudPrefab);
            _radarGo.transform.SetParent(playerCamera.transform);
            Radar.Log.LogInfo("Radar instantiated");
            _radarGo.AddComponent<HaloRadar>();

            // Create a RenderTexture
            radarRenderTexture = new RenderTexture(256, 256, 16);
            radarRenderTexture.Create();

            // Create a new camera for rendering the radar HUD
            GameObject radarCameraObject = new GameObject("RadarCamera");
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

            // Ensure the Canvas is set to World Space
            Canvas radarCanvas = _radarGo.GetComponentInChildren<Canvas>();
            if (radarCanvas != null)
            {
                radarCanvas.renderMode = RenderMode.WorldSpace;
                radarCanvas.worldCamera = radarCamera;
            }

            // Set the radar HUD to be rendered by the radar camera
            _radarGo.transform.SetParent(radarCamera.transform, false);
            _radarGo.transform.localPosition = Vector3.zero;

            // Adjust the size and position of the radar HUD if necessary
            RectTransform radarHudRectTransform = _radarGo.GetComponent<RectTransform>();
            if (radarHudRectTransform != null)
            {
                radarHudRectTransform.localScale = new Vector2(0.1f, 0.1f);  // Adjust size as needed
                radarHudRectTransform.localPosition = Vector3.zero;
            }
        }

        private void OnEnable()
        {
            Radar.radarEnableConfig.SettingChanged += OnRadarEnableChanged;
            UpdateRadarStatus();
        }

        private void OnDisable()
        {
            Radar.radarEnableConfig.SettingChanged -= OnRadarEnableChanged;
        }

        private void OnRadarEnableChanged(object sender, EventArgs e)
        {
            UpdateRadarStatus();
        }

        private void UpdateRadarStatus()
        {
            if (_radarGo != null)
            {
                _radarGo.SetActive(Radar.radarEnableConfig.Value);
            }
            else
            {
                // Radar game object is null or is destroyed
                Radar.Log.LogWarning("Radar did not load properly or has been destroyed");
                Destroy(gameObject);
            }

        }

        private void LateUpdate()
        {
            if (compassOn)
            {
                Vector3 compassPosition = compassGlass.transform.position;
                radarCamera.transform.position = compassPosition;
                radarCamera.transform.rotation = compassGlass.transform.rotation;
            }
        }

        private void Update()
        {
            // enable radar shortcut process
            if (!_enableSCDown && Radar.radarEnableShortCutConfig.Value.IsDown())
            {
                Radar.radarEnableConfig.Value = !Radar.radarEnableConfig.Value;
                _enableSCDown = true;
            }
            if (!Radar.radarEnableShortCutConfig.Value.IsDown())
            {
                _enableSCDown = false;
            }

            // enable corpse shortcut process
            if (!_corpseSCDown && Radar.radarEnableCorpseShortCutConfig.Value.IsDown())
            {
                Radar.radarEnableCorpseConfig.Value = !Radar.radarEnableCorpseConfig.Value;
                _corpseSCDown = true;
            }
            if (!Radar.radarEnableCorpseShortCutConfig.Value.IsDown())
            {
                if (compassGlass == null)
                {
                    Debug.LogError("is null");
                }
                else
                {
                    Debug.LogError("not null");
                }
                _corpseSCDown = false;
            }

            // enable loot shortcut process
            if (!_lootSCDown && Radar.radarEnableLootShortCutConfig.Value.IsDown())
            {
                Radar.radarEnableLootConfig.Value = !Radar.radarEnableLootConfig.Value;
                _lootSCDown = true;
            }
            if (!Radar.radarEnableLootShortCutConfig.Value.IsDown())
            {
                _lootSCDown = false;
            }

            compassGlass = GameObject.Find("compas_glass_LOD0");

            if (compassGlass != null)
            {
                compassOn = true;
                Renderer compassRenderer = compassGlass.GetComponent<Renderer>();
                if (compassRenderer != null)
                {
                    compassRenderer.material.mainTexture = radarRenderTexture;
                }
            }
            else
            {
                compassOn = false;
            }
        }
    }
}