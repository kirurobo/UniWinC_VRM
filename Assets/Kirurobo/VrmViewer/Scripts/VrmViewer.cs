/*
 * VrmViewer
 * 
 * Author: Kirurobo http://twitter.com/kirurobo
 * License: MIT
 */

using System;
using System.IO;
using UniHumanoid;
using UnityEngine;
using UnityEngine.Networking;
using UniVRM10;
using UniGLTF;
using UniGLTF.Extensions.VRMC_vrm;

namespace Kirurobo
{

    /// <summary>
    /// VRMビューア
    /// </summary>
    public class VrmViewer : MonoBehaviour
    {

        private UniWindowController windowController;

        private HumanPoseTransfer model;
        private HumanPoseTransfer motion;
        private VRM10ObjectMeta meta;

        public VrmUiController uiController;
        public CameraController cameraController;
        public Transform cameraTransform;

        private CameraController.ZoomType _originalZoomType;

        public AudioSource audioSource;

        public RuntimeAnimatorController animatorController;
        private Vrm10Instance vrmInstance;

        public VrmCharacterBehaviour.MotionMode motionMode
        {
            get { return _motionMode; }
            set { _motionMode = value; }
        }

        private VrmCharacterBehaviour.MotionMode _motionMode = VrmCharacterBehaviour.MotionMode.Default;


        // Use this for initialization
        void Start()
        {
            // 指定がなければ自動で探す
            if (!uiController)
            {
                uiController = FindAnyObjectByType<VrmUiController>();
            }

            uiController.enableRandomMotion = true;
            uiController.enableRandomEmotion = true;
            //if (uiController.motionToggleRandom)
            //{
            //    uiController.motionToggleRandom.onValueChanged.AddListener(val => SetRandomMotion(val));
            //}

            if (uiController.emotionToggleRandom)
            {
                uiController.emotionToggleRandom.onValueChanged.AddListener(val => SetRandomEmotion(val));
            }

            // 指定がなければ自動で探す
            if (!cameraController)
            {
                cameraController = FindAnyObjectByType<CameraController>();
                if (cameraController)
                {
                    _originalZoomType = cameraController.zoomType;
                }
            }

            // 指定がなければ自動で探す
            if (!audioSource)
            {
                audioSource = FindAnyObjectByType<AudioSource>();
            }

            // Load the initial model.
            LoadModel(Application.streamingAssetsPath + "/default.vrm");

            //// 引数でオプションが渡る場合の処理が面倒なため、引数でモデル指定は無しとする
            //string[] cmdArgs = System.Environment.GetCommandLineArgs();
            //if (cmdArgs.Length > 1)
            //{
            //	LoadModel(cmdArgs[1]);
            //} else
            //{
            //	LoadModel(Application.streamingAssetsPath + "/default_vrm.vrm");
            //}

            //// Load the default motion.
            //LoadMotion(Application.streamingAssetsPath + "/default_bvh.txt");

            // Initialize window manager
            windowController = FindAnyObjectByType<UniWindowController>();
            if (windowController)
            {
                // Add a file drop handler.
                windowController.OnDropFiles += Window_OnFilesDropped;

                if (uiController && uiController.openButton)
                {
                    uiController.openButton.onClick.AddListener(() =>
                    {
                        FilePanel.OpenFilePanel(
                            new FilePanel.Settings()
                            {
                                title = "Open VRM file",
                                filters = new FilePanel.Filter[]
                                {
                                    new FilePanel.Filter("VRM files (*.vrm)", "vrm"),
                                    //new FilePanel.Filter("All files (*.*)", "*"),
                                },
                            },
                            (path) => {
                                if (path.Length > 0) LoadFile(path[0]);
                            });
                    });
                }
            }
        }

        void Update()
        {
            // 透明なところではホイール操作は受け付けなくする
            if (windowController && cameraController)
            {
                Vector2 pos = Input.mousePosition;
                bool inScreen = (pos.x >= 0 && pos.x < Screen.width && pos.y >= 0 && pos.y < Screen.height);
                if (!windowController.isClickThrough && inScreen)
                {
                    if (uiController) _originalZoomType = uiController.zoomType;
                    cameraController.zoomType = _originalZoomType;
                }
                else
                {
                    cameraController.zoomType = CameraController.ZoomType.None;
                }
            }

            // UIで変化があったら反映させる
            if (uiController && windowController)
            {
                // 透明化方式がUIで変更されていれば反映
                if (uiController.transparentType != windowController.transparentType)
                {
                    windowController.SetTransparentType(uiController.transparentType);
                }

                // ヒットテスト方式がUIで変更されていれば反映
                if (uiController.hitTestType != windowController.hitTestType)
                {
                    windowController.hitTestType = uiController.hitTestType;
                }
            }

            // [T] キーを押すとウィンドウ透過切替
            if (Input.GetKeyDown(KeyCode.T))
            {
                windowController.isTransparent = !windowController.isTransparent;
            }

            // [O] キーを押すと最前面切替
            if (Input.GetKeyDown(KeyCode.O))
            {
                windowController.isTopmost = !windowController.isTopmost;
            }

            // [F] キーを押すと最大化切替
            if (Input.GetKeyDown(KeyCode.F))
            {
                windowController.isZoomed = !windowController.isZoomed;
            }
        }

        /// <summary>
        /// A handler for file dropping.
        /// </summary>
        /// <param name="files"></param>
        private void Window_OnFilesDropped(string[] files)
        {
            foreach (string path in files)
            {
                LoadFile(path);
            }
        }

        private void SetRandomMotion(bool enabled)
        {
            SetMotion(motion, model, meta);
        }

        private void SetRandomEmotion(bool enabled)
        {
            SetMotion(motion, model, meta);
        }

        /// <summary>
        /// ファイルを一つ読み込み
        /// VRM, BVH, 音声 に対応
        /// </summary>
        /// <param name="path"></param>
        private void LoadFile(string path)
        {
            // パスがnullなら何もしない
            if (path == null) return;

            // 拡張子を小文字で取得
            string ext = path.Substring(path.Length - 4).ToLower();

            // Open the VRM file if its extension is ".vrm".
            if (ext == ".vrm")
            {
                LoadModel(path);
                return;
            }

            //// Open the motion file if its extension is ".bvh" or ".txt".
            //if (ext == ".bvh" || ext == ".txt")
            //{
            //    LoadMotion(path);
            //    return;
            //}

            //// Open the audio file.
            //// mp3はライセンスの関係でWindowsスタンドアローンでは読み込めないよう。
            //// 参考 https://docs.unity3d.com/jp/460/ScriptReference/WWW.GetAudioClip.html
            //// 参考 https://answers.unity.com/questions/433428/load-mp3-from-harddrive-on-pc-again.html
            //if (ext == ".ogg")
            //{
            //    LoadAudio(path, AudioType.OGGVORBIS);
            //    return;
            //}
            //else if (ext == ".wav")
            //{
            //    LoadAudio(path, AudioType.WAV);
            //    return;
            //}
        }

        /// <summary>
        /// Apply the motion to the model.
        /// </summary>
        /// <param name="motion"></param>
        /// <param name="model"></param>
        /// <param name="meta"></param>
        private void SetMotion(HumanPoseTransfer motion, HumanPoseTransfer model, VRM10ObjectMeta meta)
        {
            if (!model || meta == null) return;

            var characterController = model.GetComponent<VrmCharacterBehaviour>();

            // Apply the motion if AllowedUser is equal to "Everyone".
            if (meta.AvatarPermission == AvatarPermissionType.everyone)
            {
                //_motionMode = VrmCharacterBehaviour.MotionMode.Default;
                if (uiController)
                {
                    _motionMode = uiController.motionMode;
                }

                //var anim = model.GetComponent<Animator>();
                //if (anim && this.animatorController)
                //{
                //    //anim.runtimeAnimatorController = this.animatorController;
                //    anim.runtimeAnimatorController = (RuntimeAnimatorController)RuntimeAnimatorController.Instantiate(this.animatorController);
                //    anim.applyRootMotion = true;
                //}
                //characterController.SetAnimator(anim);
                characterController.SetMotionMode(_motionMode);

            }
            else
            {
                characterController.SetMotionMode(VrmCharacterBehaviour.MotionMode.Default);
                _motionMode = VrmCharacterBehaviour.MotionMode.Default;
                
                if (uiController) {
                    uiController.enableRandomEmotion = false;
                }
            }
        }

        /// <summary>
        /// Unload the old model and load the new model from VRM file.
        /// </summary>
        /// <param name="path"></param>
        private async void LoadModel(string path)
        {
            if (!File.Exists(path))
            {
                Debug.Log("Model " + path + " is not exits.");
                return;
            }

            GameObject newModelObject = null;

            try
            {
                vrmInstance = await Vrm10.LoadPathAsync(
                    path: path,
                    canLoadVrm0X: true,
                    showMeshes: false,
                    vrmMetaInformationCallback: uiController.MetaLoaded
                    );

                newModelObject = vrmInstance.gameObject;

                var instance = vrmInstance.GetComponent<RuntimeGltfInstance>();
                instance.ShowMeshes();
                instance.EnableUpdateWhenOffscreen();

            }
            catch (Exception ex)
            {
                if (uiController) uiController.ShowWarning("Model load failed.");
                Debug.LogError("Failed loading " + path);
                Debug.LogError(ex);
                return;
            }

            if (newModelObject)
            {
                if (model)
                {
                    GameObject.Destroy(model.gameObject);
                }

                model = newModelObject.AddComponent<HumanPoseTransfer>();

                CreateColliders(model.gameObject);

                var characterController = model.gameObject.AddComponent<VrmCharacterBehaviour>();
                characterController.runtimeAnimatorController = this.animatorController;

                SetMotion(motion, model, meta);

                if (uiController)
                {
                    uiController.Show();

                    if (characterController)
                    {
                        uiController.enableRandomMotion = characterController.randomMotion;
                    }

                }
            }
        }
        
        /// <summary>
        /// Add colliders
        /// </summary>
        /// <see cref="https://qiita.com/Yuzu_Unity/items/b645ecb76816b4f44cf9"/>
        /// <param name="humanoidObject"></param>
        private void CreateColliders(GameObject humanoidObject)
        {
            var colliderBuilder = model.gameObject.AddComponent<HumanoidColliderBuilder>();
            colliderBuilder.colliderPrm.arm = new HumanoidColliderBuilder.TagLayer();
            colliderBuilder.colliderPrm.body = new HumanoidColliderBuilder.TagLayer();
            colliderBuilder.colliderPrm.head = new HumanoidColliderBuilder.TagLayer();
            colliderBuilder.colliderPrm.leg = new HumanoidColliderBuilder.TagLayer();
            colliderBuilder.colliderObj = new System.Collections.Generic.List<GameObject>();
            colliderBuilder.anim = model.GetComponent<Animator>();
            colliderBuilder.SetCollider();
        }

        /// <summary>
        /// Load the audio clip
        /// Reference: http://fantom1x.blog130.fc2.com/blog-entry-299.html
        /// </summary>
        /// <param name="path"></param>
        private void LoadAudio(string path, AudioType audioType)
        {
            StartCoroutine(LoadAudioCoroutine(path, audioType));
        }

        private System.Collections.IEnumerator LoadAudioCoroutine(string path, AudioType audioType)
        {
            if (!File.Exists(path)) yield break;

            using (var www = UnityWebRequestMultimedia.GetAudioClip("file://" + path, audioType))
            {
                while (!www.isDone)
                {
                    yield return null;
                }

                var audioClip = DownloadHandlerAudioClip.GetContent(www);
                if (audioClip.loadState != AudioDataLoadState.Loaded)
                {
                    Debug.Log("Failed to load audio: " + path);
                    yield break;
                }

                audioSource.clip = audioClip;
                audioSource.Play();
                Debug.Log("Audio: " + path);
            }
        }
    }
}