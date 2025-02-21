using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using UniVRM10;
using System.Linq;

namespace Kirurobo
{

    public class VrmUiController : MonoBehaviour
    {
        /// <summary>
        /// セーブ情報のバージョン番号
        /// </summary>
        const float prefsVersion = 0.02f;

        [HideInInspector]
        public UniWindowController windowController;
        private CameraController cameraController;

        public RectTransform panel;
        public Text informationText;
        public Text warningText;
        public Button closeButton;
        public Toggle transparentToggle;
        public Toggle topmostToggle;

        [FormerlySerializedAs("maximizedToggle")]
        public Toggle zoomedToggle;
        public Button openButton;
        public Button quitButton;
        public Text titleText;
        public Dropdown zoomTypeDropdown;

        public Dropdown transparentTypeDropdown;
        public Dropdown hitTestTypeDropdown;
        public Dropdown languageDropdown;
        public Toggle motionTogglePreset;
        public Toggle motionToggleRandom;
        public Toggle motionToggleDance;
        public Slider volumeSlider;
        public Toggle emotionToggleRandom;
        public Dropdown blendShapeDropdown;
        public Slider blendShapeSlider;

        public Button tabButtonModel;
        public Button tabButtonControl;
        public RectTransform modelPanel;
        public RectTransform controlPanel;
        public CameraController.ZoomType zoomType { get; set; }
        public UniWindowController.TransparentType transparentType { get; set; }
        public UniWindowController.HitTestType hitTestType { get; set; }
        int language { get; set; }

        private float mouseMoveSS = 0f; // Sum of mouse trajectory squares. [px^2]
        private float mouseMoveSSThreshold = 16f; // Threshold to be regarded as not moving. [px^2]
        private Vector3 lastMousePosition;

        private Vector2 originalAnchoredPosition;
        private Canvas canvas;

        private VrmUiLocale uiLocale;

        private TabPanelManager tabPanelManager;

        public delegate void motionChangedDelegate(VrmCharacterBehaviour.MotionMode mode);
        public motionChangedDelegate OnMotionChanged;

        public delegate void blendShapeChangedDelegate(int index = -1, float value = -1f);
        public blendShapeChangedDelegate OnBlendShapeChanged;

        private AudioSource audioSource;

        public Material uiMaterial;

        // プレビュー部分のオブジェクト
        public Text previewVrmVersion;
        public RawImage previewImage;
        public Text previewText;
        public Text previewVersion;
        public Text previewAuthor;
        public Text previewContact;
        public Text previewReference;



        /// <summary>
        /// ランダムモーションが有効かを取得／設定
        /// </summary>
        public bool enableRandomMotion
        {
            get
            {
                if (motionToggleRandom) return motionToggleRandom.isOn;
                return false;
            }
            set
            {
                if (motionToggleRandom) motionToggleRandom.isOn = value;
            }
        }

        /// <summary>
        /// ランダム表情が有効かを取得／設定
        /// </summary>
        public bool enableRandomEmotion
        {
            get
            {
                if (emotionToggleRandom) return emotionToggleRandom.isOn;
                return false;
            }
            set
            {
                if (emotionToggleRandom) emotionToggleRandom.isOn = value;
                if (blendShapeDropdown) blendShapeDropdown.interactable = !value;
                if (blendShapeSlider) blendShapeSlider.interactable = !value;
            }
        }

        public VrmCharacterBehaviour.MotionMode motionMode
        {
            get
            {
                if (motionToggleRandom && motionToggleRandom.isOn) return VrmCharacterBehaviour.MotionMode.Random;
                if (motionToggleDance && motionToggleDance.isOn) return VrmCharacterBehaviour.MotionMode.Dance;
                return VrmCharacterBehaviour.MotionMode.Default;
            }
            set
            {
                //if (value == VrmCharacterBehaviour.MotionMode.Random)
                //{
                //    if (motionTogglePreset) motionTogglePreset.isOn = false;
                //    if (motionToggleRandom) motionToggleRandom.isOn = true;
                //    if (motionToggleBvh) motionToggleBvh.isOn = false;
                //}
                //else if (value == VrmCharacterBehaviour.MotionMode.Bvh)
                //{
                //    if (motionTogglePreset) motionTogglePreset.isOn = false;
                //    if (motionToggleRandom) motionToggleRandom.isOn = false;
                //    if (motionToggleBvh) motionToggleBvh.isOn = true;
                //}
                //else
                //{
                //    if (motionTogglePreset) motionTogglePreset.isOn = true;
                //    if (motionToggleRandom) motionToggleRandom.isOn = false;
                //    if (motionToggleBvh) motionToggleBvh.isOn = false;
                //}

                if (OnMotionChanged != null)
                {
                    OnMotionChanged.Invoke(value);
                }
            }
        }

        public void OnMotionToggleClicked(VrmCharacterBehaviour.MotionMode value)
        {
            if (value == VrmCharacterBehaviour.MotionMode.Random)
            {
                if (motionTogglePreset) motionTogglePreset.isOn = false;
                if (motionToggleDance) motionToggleDance.isOn = false;
            }
            else if (value == VrmCharacterBehaviour.MotionMode.Dance)
            {
                if (motionTogglePreset) motionTogglePreset.isOn = false;
                if (motionToggleRandom) motionToggleRandom.isOn = false;
            }
            else
            {
                if (motionToggleRandom) motionToggleRandom.isOn = false;
                if (motionToggleDance) motionToggleDance.isOn = false;
            }

            motionMode = value;
        }

        void ApplyUiTextMaterial()
        {
            if (!uiMaterial) return;

            var list = gameObject.GetComponentsInChildren<Text>();
            foreach (var obj in list)
            {
                obj.material = uiMaterial;
            }
        }

        private void Awake()
        {
            ApplyUiTextMaterial();
        }

        /// <summary>
        /// Use this for initialization
        /// </summary>
        void Start()
        {
            if (!canvas)
            {
                canvas = GetComponent<Canvas>();
            }

            zoomType = CameraController.ZoomType.Zoom;
            transparentType = UniWindowController.TransparentType.Alpha;

            // WindowControllerが指定されていなければ自動取得
            windowController = FindAnyObjectByType<UniWindowController>();
            if (windowController)
            {
                windowController.OnStateChanged += windowController_OnStateChanged;

                transparentType = windowController.transparentType;
            }

            // カメラ操作スクリプト
            cameraController = FindAnyObjectByType<CameraController>();

            uiLocale = this.GetComponentInChildren<VrmUiLocale>();
            tabPanelManager = this.GetComponentInChildren<TabPanelManager>();

            // 中央基準にする
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            originalAnchoredPosition = panel.anchoredPosition;

            // 表情の選択肢を準備
            SetupBlendShapeDropdown();

            // Load settings.
            Load();

            // Initialize toggles.
            UpdateUI();

            // Set event listeners.
            if (closeButton)
            {
                closeButton.onClick.AddListener(Close);
            }

            if (quitButton)
            {
                quitButton.onClick.AddListener(Quit);
            }

            if (windowController)
            {
                // プロパティをバインド
                if (transparentToggle)
                {
                    transparentToggle.onValueChanged.AddListener((value => windowController.isTransparent = value));
                }

                if (zoomedToggle)
                {
                    zoomedToggle.onValueChanged.AddListener((value => windowController.isZoomed = value));
                }

                if (topmostToggle)
                {
                    topmostToggle.onValueChanged.AddListener((value => windowController.isTopmost = value));
                }
            }

            // タイトルのバージョン番号を追加
            if (titleText)
            {
                titleText.text = Application.productName + " Ver." + Application.version;
            }

            if (emotionToggleRandom) { emotionToggleRandom.onValueChanged.AddListener(val => enableRandomEmotion = val); }
            if (motionTogglePreset) { motionTogglePreset.onValueChanged.AddListener(val => OnMotionToggleClicked(VrmCharacterBehaviour.MotionMode.Default)); }
            if (motionToggleDance) { motionToggleDance.onValueChanged.AddListener(val => OnMotionToggleClicked(VrmCharacterBehaviour.MotionMode.Dance)); }
            //if (motionToggleRandom) { motionToggleRandom.onValueChanged.AddListener(val => motionMode = VrmCharacterBehaviour.MotionMode.Random); }

            // 表情の選択肢が変更されたときの処理
            if (blendShapeDropdown)
            {
                blendShapeDropdown.onValueChanged.AddListener(OnBlendShapeIndexChanged);
            }
            // 表情スライダーの値が変更されたときの処理
            if (blendShapeSlider)
            {
                blendShapeSlider.onValueChanged.AddListener(OnBlendShapeSliderChanged);
            }

            // 直接バインドしない項目の初期値とイベントリスナーを設定
            if (zoomTypeDropdown)
            {
                zoomTypeDropdown.value = (int) zoomType;
                zoomTypeDropdown.onValueChanged.AddListener(val => SetZoomType(val));
            }

            if (transparentTypeDropdown)
            {
                transparentTypeDropdown.value = (int) transparentType;
                transparentTypeDropdown.onValueChanged.AddListener(val => SetTransparentType(val));
            }

            if (hitTestTypeDropdown)
            {
                hitTestTypeDropdown.value = (int) hitTestType;
                hitTestTypeDropdown.onValueChanged.AddListener(val => SetHitTestType(val));
            }

            if (languageDropdown)
            {
                languageDropdown.value = language;
                languageDropdown.onValueChanged.AddListener(val => SetLanguage(val));
            }

            if (volumeSlider)
            {
                // AudioSourceの最大音量
                const float maxSourceVolume = 0.2f;

                audioSource = FindAnyObjectByType<AudioSource>();

                // 今の設定で音量を調整しておく
                audioSource.volume = maxSourceVolume * volumeSlider.value;

                if (audioSource) {
                    volumeSlider.onValueChanged.AddListener(val => audioSource.volume = (maxSourceVolume * val));
                 }
            }

            // Show menu on startup.
            Show();
        }

        public void Save()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.SetFloat("Version", prefsVersion);

            if (windowController)
            {
                PlayerPrefs.SetInt("Transparent", windowController.isTransparent ? 1 : 0);
                PlayerPrefs.SetInt("Maximized", windowController.isZoomed ? 1 : 0);
                PlayerPrefs.SetInt("Topmost", windowController.isTopmost ? 1 : 0);
            }

            PlayerPrefs.SetInt("ZoomType", (int) zoomType);
            PlayerPrefs.SetInt("TransparentType", (int) transparentType);
            PlayerPrefs.SetInt("HitTestType", (int) hitTestType);
            PlayerPrefs.SetInt("Language", language);

            PlayerPrefs.SetInt("VrmCharacterBehaviour.MotionMode", (int) motionMode);
            PlayerPrefs.SetInt("EmotionMode", enableRandomEmotion ? 1 : 0);
            
            if (volumeSlider) PlayerPrefs.SetFloat("Volume", volumeSlider.value);
        }

        public void Load()
        {
            //// セーブされた情報のバージョンが異なれば読み出さない
            //if (PlayerPrefs.GetFloat("Version") != prefsVersion) return;

            int defaultTransparentTypeIndex = 1;
            int defaultHitTestTypeIndex = 1;
            int defaultZoomTypeIndex = 0;
            int defaultLanguageIndex = 0;

            // UIで設定されている値を最初にデフォルトとする
            if (zoomTypeDropdown) defaultZoomTypeIndex = zoomTypeDropdown.value;
            if (transparentTypeDropdown) defaultTransparentTypeIndex = transparentTypeDropdown.value;
            if (languageDropdown) defaultLanguageIndex = languageDropdown.value;

            if (windowController)
            {
                windowController.isTransparent = LoadPrefsBool("Transparent", windowController.isTransparent);
                windowController.isZoomed = LoadPrefsBool("Maximized", windowController.isZoomed);
                windowController.isTopmost = LoadPrefsBool("Topmost", windowController.isTopmost);

                // WindowControllerの値をデフォルトとする
                defaultTransparentTypeIndex = (int) windowController.transparentType;
                defaultHitTestTypeIndex = (int) windowController.hitTestType;
            }

            SetZoomType(PlayerPrefs.GetInt("ZoomType", defaultZoomTypeIndex));
            SetTransparentType(PlayerPrefs.GetInt("TransparentType", defaultTransparentTypeIndex));
            SetHitTestType(PlayerPrefs.GetInt("HitTestType", defaultHitTestTypeIndex));
            SetLanguage(PlayerPrefs.GetInt("Language", defaultLanguageIndex));

            if (volumeSlider) volumeSlider.value = PlayerPrefs.GetFloat("Volume", volumeSlider.value);

            motionMode =
                (VrmCharacterBehaviour.MotionMode) PlayerPrefs.GetInt("VrmCharacterBehaviour.MotionMode",
                    (int) motionMode);
            enableRandomEmotion = LoadPrefsBool("EmotionMode", enableRandomEmotion);
        }

        private bool LoadPrefsBool(string name, bool currentVal)
        {
            int pref = PlayerPrefs.GetInt(name, -1);
            if (pref < 0) return currentVal; // データがないか-1なら元の値のまま
            return (pref > 0); // そうでなければ 0:false , 1以上:true を返す
        }

        /// <summary>
        /// マウスホイールでのズーム方法を選択
        /// </summary>
        /// <param name="no">選択肢の番号（Dropdownを編集したら下記も要編集）</param>
        private void SetZoomType(int no)
        {
            if (no == 1)
            {
                zoomType = CameraController.ZoomType.Dolly;
            }
            else
            {
                zoomType = CameraController.ZoomType.Zoom;
            }
        }

        /// <summary>
        /// ウィンドウ透過方式を選択
        /// </summary>
        /// <param name="index">選択肢の番号（Dropdownを編集したら下記も要編集）</param>
        private void SetTransparentType(int index)
        {
            if (index == 1)
            {
                transparentType = UniWindowController.TransparentType.Alpha;
            }
            else if (index == 2)
            {
                transparentType = UniWindowController.TransparentType.ColorKey;
            }
            else
            {
                transparentType = UniWindowController.TransparentType.None;
            }
        }

        /// <summary>
        /// ヒットテスト方式を選択
        /// </summary>
        /// <param name="index">選択肢の番号（Dropdownを編集したら下記も要編集）</param>
        private void SetHitTestType(int index)
        {
            if (index == 1)
            {
                hitTestType = UniWindowController.HitTestType.Opacity;
            }
            else if (index == 2)
            {
                hitTestType = UniWindowController.HitTestType.Raycast;
            }
            else
            {
                hitTestType = UniWindowController.HitTestType.None;
            }
        }

        /// <summary>
        /// UI言語選択
        /// </summary>
        /// <param name="index">選択肢の番号（Dropdownを編集したら下記も要編集）</param>
        private void SetLanguage(int index)
        {
            string lang;
            switch (index)
            {
                case 1:
                    lang = "ja";
                    language = 1;
                    break;
                default:
                    lang = "en";
                    language = 0;
                    break;
            }

            if (uiLocale) uiLocale.SetLocale(lang);
        }

        /// <summary>
        /// 表情の選択肢を初期化
        /// </summary>
        private void SetupBlendShapeDropdown()
        {
            if (!blendShapeDropdown) return;

            blendShapeDropdown.options.Clear();
            foreach (var preset in VrmCharacterBehaviour.EmotionPresets)
            {
                blendShapeDropdown.options.Add(new Dropdown.OptionData(preset.ToString()));
            }

            blendShapeDropdown.RefreshShownValue();
        }

        /// <summary>
        /// Apply the dropdown index
        /// </summary>
        /// <param name="index"></param>
        internal void SetBlendShape(int index)
        {
            if (blendShapeDropdown)
            {
                blendShapeDropdown.value = index;
                blendShapeDropdown.RefreshShownValue();
            }
        }

        /// <summary>
        /// Apply the slide value
        /// </summary>
        /// <param name="value"></param>
        internal void SetBlendShapeValue(float value)
        {
            if (blendShapeSlider)
            {
                blendShapeSlider.value = value;
            }
        }

        /// <summary>
        /// 表情の選択肢が変更されたときの処理
        /// </summary>
        private void OnBlendShapeIndexChanged(int index) {
            // 表情がランダムになっている場合は何もしない
            if (emotionToggleRandom && emotionToggleRandom.isOn) return;

            if (OnBlendShapeChanged != null)
            {
                OnBlendShapeChanged.Invoke(index, -1f);
            }
        }

        /// <summary>
        /// 表情スライダーの値が変更されたときの処理
        /// </summary>
        /// <param name="value">-1fだと変更しない</param>
        private void OnBlendShapeSliderChanged(float value)
        {
            // 表情がランダムになっている場合は何もしない
            if (emotionToggleRandom && emotionToggleRandom.isOn) return;

            if (OnBlendShapeChanged != null)
            {
                OnBlendShapeChanged.Invoke(-1, value);
            }
        }

        private void windowController_OnStateChanged(UniWindowController.WindowStateEventType type)
        {
            UpdateUI();
            //if (windowController.isReady) isFirstUpdate = false;
        }

        /// <summary>
        /// UIの状況を現在のウィンドウ状態に合わせて更新
        /// </summary>
        public void UpdateUI()
        {
            if (windowController)
            {
                if (transparentToggle)
                {
                    transparentToggle.isOn = windowController.isTransparent;
                }

                if (zoomedToggle)
                {
                    zoomedToggle.isOn = windowController.isZoomed;
                }

                if (topmostToggle)
                {
                    topmostToggle.isOn = windowController.isTopmost;
                }
            }

        }


        /// <summary>
        /// メニューを閉じる
        /// </summary>
        private void Close()
        {
            panel.gameObject.SetActive(false);
            //Debug.Log("Close. Zoom:" + zoomMode + ", Trans.:" + transparentType + ", Lang.:" + language);

            if (cameraController) cameraController.enableWheel = true;
        }

        /// <summary>
        /// 終了ボタンが押された時の処理。エディタ上であれば再生停止とする。
        /// </summary>
        private void Quit()
        {
#if UNITY_EDITOR
            // Stop playing for the editor
            UnityEditor.EditorApplication.isPlaying = false;
#else
        // Quit application for the standalone player
        Application.Quit();
#endif
        }

        void OnApplicationQuit()
        {
            // 終了時には設定を保存する
            Save();
            //Debug.Log("Saved. Zoom:" + zoomMode + ", Trans.:" + transparentType + ", Lang.:" + language);
        }

        /// <summary>
        /// Update is called once per frame
        /// </summary>
        void Update()
        {
            // マウス右ボタンクリックでメニューを表示させる。閾値以下の移動ならクリックとみなす。
            if (Input.GetMouseButtonDown(1))
            {
                lastMousePosition = Input.mousePosition;
            }

            if (Input.GetMouseButton(1))
            {
                mouseMoveSS += (Input.mousePosition - lastMousePosition).sqrMagnitude;
            }

            if (Input.GetMouseButtonUp(1))
            {
                if (mouseMoveSS < mouseMoveSSThreshold)
                {
                    Show(lastMousePosition);
                }

                mouseMoveSS = 0f;
            }

            // [ESC] でメニューを閉じる
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        /// <summary>
        /// フォーカスが外れたときの処理
        /// </summary>
        /// <param name="focus"></param>
        private void OnApplicationFocus(bool focus)
        {
            // フォーカスが外れたらメニューを閉じる
            if (!focus)
            {
                Close();
            }
        }

        /// <summary>
        /// 座標を指定してメニューを表示する
        /// </summary>
        /// <param name="mousePosition"></param>
        public void Show(Vector2 mousePosition)
        {
            if (panel)
            {
                Vector2 pos = mousePosition;
                float w = panel.rect.width;
                float h = panel.rect.height;

                pos.x += Mathf.Max(w * 0.5f - pos.x, 0f); // 左にはみ出していれば右に寄せる
                pos.y += Mathf.Max(h * 0.5f - pos.y, 0f); // 下にはみ出していれば上に寄せる
                pos.x -= Mathf.Max(pos.x - Screen.width + w * 0.5f, 0f); // 右にはみ出していれば左に寄せる
                pos.y -= Mathf.Max(pos.y - Screen.height + h * 0.5f, 0f); // 上にはみ出していれば下に寄せる

                panel.anchorMin = Vector2.zero;
                panel.anchorMax = Vector2.zero;
                panel.anchoredPosition = pos;
                panel.gameObject.SetActive(true);
            }

            if (cameraController) cameraController.enableWheel = false;
        }

        /// <summary>
        /// メニューを表示する
        /// </summary>
        public void Show()
        {
            if (panel)
            {
                // 中央基準にして表示
                panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
                panel.anchoredPosition = originalAnchoredPosition;
                panel.gameObject.SetActive(true);
            }

            if (cameraController) cameraController.enableWheel = false;
        }

        public void MetaLoaded(Texture2D thumbnail, UniGLTF.Extensions.VRMC_vrm.Meta meta, UniVRM10.Migration.Vrm0Meta vrm0Meta)
        {
            if (thumbnail)
            {
                previewImage.texture = thumbnail;
            }

            if (meta != null)
            {
                previewVrmVersion.text = "VRM 1";
                previewText.text = meta.Name;
                previewVersion.text = meta.Version;
                previewAuthor.text = meta.Authors?.ToString();
                previewContact.text = meta.ContactInformation;
                previewReference.text = meta.References?.ToString();
            } else if (vrm0Meta != null) {
                previewVrmVersion.text = "VRM 0";
                previewText.text = vrm0Meta.title;
                previewVersion.text = vrm0Meta.version;
                previewAuthor.text = vrm0Meta.author;
                previewContact.text = vrm0Meta.contactInformation;
                previewReference.text = vrm0Meta.reference;
            }
            
            tabPanelManager.Select(0); // 0番がモデル情報のパネルという前提で、それを開く
        }

        /// <summary>
        /// Set the warning text
        /// </summary>
        /// <param name="message"></param>
        public void ShowWarning(string message)
        {
            if (warningText)
            {
                warningText.text = message;
            }
        }
    }
}