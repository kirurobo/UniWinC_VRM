/*
 * VrmCharacterBehaviour
 * 
 * キャラクターのまばたきや表情、動作を制御します
 * 
 * Author: Kirurobo http://twitter.com/kirurobo
 * License: MIT
 */

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Android;
using UniVRM10;

namespace Kirurobo
{

    public class VrmCharacterBehaviour : MonoBehaviour
    {

        public float LookAtSpeed = 10.0f; // 頭の追従速度係数 [1/s]
        private float BlinkTime = 0.1f; // まばたきで閉じるまたは開く時間 [s]

        private float lastBlinkTime = 0f;
        private float nextBlinkTime = 0f;
        private BlinkState blinkState = BlinkState.None; // まばたきの状態管理。 0:なし, 1:閉じ中, 2:開き中

        public enum BlinkState
        {
            None = 0, // 瞬き無効
            Closing = 1,
            Opening = 2,
        }

        public enum MotionMode
        {
            Default = 0,
            Random = 1,
            Bvh = 2,
            Dance = 3,
        }

        internal ExpressionKey[] emotionKeys;

        internal int emotionIndex = 0; // 表情の状態
        internal float emotionRate = 0f; // その表情になっている程度 0～1
        private float emotionSpeed = 0f; // 表情を発生させる方向なら 1、戻す方向なら -1、維持なら 0
        private float nextEmotionTime = 0f; // 次に表情を変化させる時刻

        public float emotionInterval = 2f; // 表情を変化させる間隔
        public float emotionIntervalRandamRange = 5f; // 表情変化間隔のランダム要素

        private float emotionPromoteTime = 0.5f; // 表情が変化しきるまでの時間 [s]

        private Vrm10RuntimeLookAt runtimeLookAt;
        private Vrm10RuntimeExpression runtimeExpression;

        private GameObject targetObject; // 視線目標オブジェクト
        private Transform headTransform; // Head transform
        private bool hasNewTargetObject = false; // 新規に目標オブジェクトを作成したらtrue
        private Transform leftHandTransform;
        private Transform rightHandTransform;
        private Transform leftShoulderTransform;
        private Transform rightShoulderTransform;

        private Animator animator;
        private AnimatorStateInfo currentState; // 現在のステート状態を保存する参照
        private AnimatorStateInfo previousState; // ひとつ前のステート状態を保存する参照
        public RuntimeAnimatorController runtimeAnimatorController;

        public MotionMode motionMode = MotionMode.Default;

        //private float cursorGrabingSqrMagnitude = 0.81f;    // 手の届く距離の2乗（これ以上離れると手を伸ばすことをやめる）
        private float cursorGrabingSqrMagnitude = 9f; // 手の届く距離の2乗（これ以上離れると手を伸ばすことをやめる）
        private float lastRightHandWeight = 0f;
        private float lastLeftHandWeight = 0f;

        public bool randomMotion = false; // モーションをランダムにするか

        private Camera currentCamera;
        private VrmUiController uiController;
        private AudioSource refAudioSource;

        // Use this for initialization
        void Start()
        {
            if (!targetObject)
            {
                targetObject = new GameObject("LookAtTarget");
                hasNewTargetObject = true;
            }

            var vrm10 = GetComponent<Vrm10Instance>();
            if (vrm10 != null)
            {
                runtimeLookAt = vrm10.Runtime.LookAt;
                runtimeExpression = vrm10.Runtime.Expression;
            }

            if (runtimeLookAt != null)
            {
                // 視線を送るオブジェクトを設定
                vrm10.LookAtTargetType = VRM10ObjectLookAt.LookAtTargetTypes.SpecifiedTransform;
                vrm10.LookAtTarget = targetObject.transform;
                
                // 頭部分を代表するTransformを取得
                headTransform = runtimeLookAt.LookAtOriginTransform;
            }

            if (runtimeExpression != null)
            {
                emotionKeys = runtimeExpression.ExpressionKeys.ToArray();
            } else {
                emotionKeys = new ExpressionKey[0];
            }

            if (!headTransform)
            {
                headTransform = this.transform;
            }

            // マウスカーソルとの奥行きに反映させるためカメラを取得
            if (!currentCamera)
            {
                currentCamera = Camera.main;
            }

            // UIコントローラーを取得
            if (!uiController)
            {
                uiController = FindAnyObjectByType<VrmUiController>();

                // 表情のドロップダウンを用意
                uiController.SetupExpressionDropdown(emotionKeys);

                // UIコントローラーのイベントを受け取る
                uiController.OnMotionChanged += SetMotionMode;
                uiController.OnExpressionChanged += SetExpression;
            }

            // AudioSourceを取得
            if (!refAudioSource)
            {
                refAudioSource = FindAnyObjectByType<AudioSource>();
            }

            SetAnimator(GetComponent<Animator>());


            currentState = animator.GetCurrentAnimatorStateInfo(0);
            previousState = currentState;
        }

        /// <summary>
        /// Destroy created target object
        /// </summary>
        void OnDestroy()
        {
            if (hasNewTargetObject)
            {
                GameObject.Destroy(targetObject);
            }
        }

        public void SetAnimator(Animator anim)
        {
            if (!anim)
            {
                rightHandTransform = null;
                leftHandTransform = null;
            }
            else if (anim != animator)
            {
                // カーソルに手を伸ばす際に利用する
                rightHandTransform = anim.GetBoneTransform(HumanBodyBones.RightHand);
                leftHandTransform = anim.GetBoneTransform(HumanBodyBones.LeftHand);
                
                // カーソルが体のどちら側にあるかを見る際は肩を見る
                rightShoulderTransform = anim.GetBoneTransform(HumanBodyBones.RightShoulder);
                leftShoulderTransform = anim.GetBoneTransform(HumanBodyBones.LeftShoulder);
                // もし肩が不明なら手で代替
                if (!rightShoulderTransform) rightShoulderTransform = rightHandTransform;
                if (!leftShoulderTransform) leftShoulderTransform = leftHandTransform;
            }

            animator = anim;
            animator.applyRootMotion = false;

            if (this.runtimeAnimatorController)
            {
                animator.runtimeAnimatorController = RuntimeAnimatorController.Instantiate(this.runtimeAnimatorController);
            }

            //animator.applyRootMotion = false;

            //animator.StartPlayback();

            if (motionMode == MotionMode.Dance)
            {
                StartDance();
            }
            else
            {
                StopDance();
            }
        }

        public void SetMotionMode(MotionMode mode)
        {
            if (!animator)
            {
                motionMode = mode;
            }
            else if (motionMode == MotionMode.Dance && mode != MotionMode.Dance)
            {
                // ダンス中からダンス以外になったら、ダンスは停止
                StopDance();
                motionMode = mode;
            }
            else if (motionMode != MotionMode.Dance && mode == MotionMode.Dance)
            {
                // ダンス以外からダンスになったら、ダンスを開始
                motionMode = mode;
                StartDance();
            }
            else
            {
                motionMode = mode;
            }
        }

        /// <summary>
        /// UI側で表情を変更されたときに呼ばれる処理
        /// </summary>
        /// <param name="index">ブレンドシェイプ番号。-1だと変更なしで量のみ更新</param>
        public void SetExpression(int index, float value = -1f)
        {
            bool updated = false;
            if (index >= 0 && index < emotionKeys.Length) {
                emotionIndex = index;
                updated = true;
            }
            if (value >= 0f && value <= 1f)
            {
                emotionRate = value;
                updated = true;
            }
            if (updated)
            {
                UpdateEmotion();
            }
        }

        /// <summary>
        /// 毎フレーム呼ばれる
        /// </summary>
        void Update()
        {
            UpdateLookAtTarget();
            Blink();
            RandomFace();
            UpdateMotion();
        }

        /// <summary>
        /// Update()より後で呼ばれる
        /// </summary>
        void LateUpdate()
        {
            UpdateHead();
        }

        /// <summary>
        /// 目線目標座標を更新
        /// </summary>
        private void UpdateLookAtTarget()
        {
            Vector3 mousePos = Input.mousePosition;
            //// 奥行きはモデル座標から 1[m] 手前に設定
            //mousePos.z = (currentCamera.transform.position - headTransform.position).magnitude - 1f;
            // 奥行きはモデル座標とカメラ間の95%と設定
            mousePos.z = (currentCamera.transform.position - headTransform.position).magnitude * 0.95f;
            Vector3 pos = currentCamera.ScreenToWorldPoint(mousePos);
            targetObject.transform.position = pos;
        }

        /// <summary>
        /// マウスカーソルの方を見る動作
        /// </summary>
        private void UpdateHead()
        {
            if (runtimeLookAt == null) return;
            Quaternion rot = Quaternion.Euler(-runtimeLookAt.Pitch, runtimeLookAt.Yaw, 0f);
            headTransform.rotation = Quaternion.Slerp(headTransform.rotation, rot, 0.2f);

        }

        /// <summary>
        /// まばたき
        /// </summary>
        private void Blink()
        {
            if (runtimeExpression == null) return;

            float now = Time.timeSinceLevelLoad;
            float span;

            float blinkValue = 0f;

            ExpressionKey blinkPresetKey = ExpressionKey.CreateFromPreset(ExpressionPreset.blink);

            //// VRM 0 では Relaxed、VRM 1 では Happy ? 明確ではないためコメントアウト
            // // 表情が笑顔の時は目が閉じられるため、まばたきは無効とする
            // if (emotionKeys[emotionIndex].Equals(ExpressionKey.Relaxed))
            // {
            //     blinkState = BlinkState.None;
            //     blinkValue = 0f;
            //     runtimeExpression.SetWeight(blinkPresetKey, blinkValue);
            // }
            
            // まばたきの状態遷移
            switch (blinkState)
            {
                case BlinkState.Closing:
                    span = now - lastBlinkTime;

                    if (span > BlinkTime)
                    {
                        blinkState = BlinkState.Opening;
                        blinkValue = 1f;
                    }
                    else
                    {
                        blinkValue = span / BlinkTime;
                    }
                    runtimeExpression.SetWeight(blinkPresetKey, blinkValue);
                    break;
                case BlinkState.Opening:
                    span = now - lastBlinkTime - BlinkTime;

                    if (span > BlinkTime)
                    {
                        blinkState = BlinkState.None;
                        blinkValue = 0f;
                    }
                    else
                    {
                        blinkValue = 1f - (span / BlinkTime);
                    }
                    runtimeExpression.SetWeight(blinkPresetKey, blinkValue);
                    break;
                default:
                    if (now >= nextBlinkTime)
                    {
                        lastBlinkTime = now;
                        if (Random.value < 0.2f)
                        {
                            nextBlinkTime = now; // 20%の確率で連続まばたき
                        }
                        else
                        {
                            nextBlinkTime = now + Random.Range(1f, 10f);
                        }

                        blinkState = BlinkState.Closing;
                    }
                    break;
            }
        
        }

        /// <summary>
        /// 表情をランダムに変更
        /// </summary>
        public void RandomFace()
        {
            float now = Time.timeSinceLevelLoad;

            if (uiController && !uiController.enableRandomEmotion)
            {
                // UIでランダム表情が無効にされていたら何もしない
                return;
            }

            if (now >= nextEmotionTime)
            {
                // 待ち時間を越えた場合の処理
                nextEmotionTime = now + emotionInterval + Random.value * emotionIntervalRandamRange;
                emotionSpeed = (emotionSpeed > 0 ? -1f : emotionSpeed < 0 ? 0 : 1f); // 表情を与えるか戻すか、次の方向を決定

                // 表情を与えるなら、ランダムで次の表情を決定
                if (emotionSpeed > 0)
                {
                    emotionIndex = Random.Range(0, emotionKeys.Length - 1);
                }
            }
            else
            {
                // 待ち時間に達していなければ、変化を処理
                float dt = Time.deltaTime;
                emotionRate = Mathf.Min(1f, Mathf.Max(0f, emotionRate + emotionSpeed * (dt / emotionPromoteTime)));

                UpdateEmotion();
            }

        }

        /// <summary>
        /// 現在の表情を適用
        /// </summary>
        private void UpdateEmotion()
        {
            if (runtimeExpression == null) return;

            var weights = new List<KeyValuePair<ExpressionKey, float>>();

            int index = 0;
            foreach (var key in emotionKeys)
            {
                float val = 0f;
                // 現在選ばれている表情のみ値を入れ、他はゼロとする
                if (index == emotionIndex) val = emotionRate;
                weights.Add(new KeyValuePair<ExpressionKey, float>(key, val));
                index++;
            }

            runtimeExpression.SetWeights(weights);

            UpdateUI();
        }

        /// <summary>
        /// UIの表情表示を更新
        /// </summary>
        private void UpdateUI()
        {
            if (!uiController) return;

            if (uiController.enableRandomEmotion)
            {
                uiController.SetExpression(emotionIndex);
                uiController.SetExpressionValue(emotionRate);
            }
        }

        /// <summary>
        /// モーション変更に関する処理をここに書く（現在はオミット）
        /// </summary>
        private void UpdateMotion()
        {
        }

        /// <summary>
        /// IK処理時に手をマウスカーソルに伸ばす
        /// </summary>
        void OnAnimatorIK()
        {
            UpdateHands();
        }
        
        /// <summary>
        /// マウスカーソルの方を見る動作
        /// </summary>
        private void UpdateHands()
        {
            if (!animator || !rightHandTransform || !leftHandTransform) return;

            // BVH再生中はスキップ
            if (motionMode == MotionMode.Dance) return;

            bool isRightHandMoved = false;
            bool isLeftHandMoved = false;

            Vector3 cursorPosition = targetObject.transform.position;

            AnimatorStateInfo animState = animator.GetCurrentAnimatorStateInfo(0);

            // IK_HANDというアニメーションのときのみ、手を伸ばす
            if (animState.IsName("IK_HAND") || animState.IsName("IK_HAND_REVERSE"))
            {
                // 手先ではなく、右肩、左肩どちらにカーソルが近いかで、左右どちらの腕を伸ばすか決定する
                float sqrDistanceRight = (cursorPosition - rightShoulderTransform.position).sqrMagnitude;
                float sqrDistanceLeft = (cursorPosition - leftShoulderTransform.position).sqrMagnitude;

                if (sqrDistanceRight < sqrDistanceLeft)
                {
                    // カーソルが右手側にある場合
                    // 右手とカーソルの距離の2乗
                    float sqrDistance = sqrDistanceRight;

                    // 右手からの距離が近ければ追従させる
                    if ((sqrDistance < cursorGrabingSqrMagnitude))
                    {
                        lastRightHandWeight = Mathf.Lerp(lastRightHandWeight, 0.9f, 0.01f);

                        animator.SetIKPosition(AvatarIKGoal.RightHand, cursorPosition);
                        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, lastRightHandWeight);

                        //// 回転
                        //Quaternion handRotation = Quaternion.Euler(-90f, 180f, 0f);
                        //animator.SetIKRotation(AvatarIKGoal.RightHand, handRotation);
                        //animator.SetIKRotationWeight(AvatarIKGoal.RightHand, lastRightHandWait);

                        isRightHandMoved = true;
                    }
                }
                else
                {
                    // カーソルが左手側にある場合
                    // 左とカーソルの距離の2乗
                    float sqrDistance = sqrDistanceLeft;

                    // 左手からの距離が近ければ追従させる
                    if ((sqrDistance < cursorGrabingSqrMagnitude))
                    {
                        lastLeftHandWeight = Mathf.Lerp(lastLeftHandWeight, 0.9f, 0.01f);

                        animator.SetIKPosition(AvatarIKGoal.LeftHand, cursorPosition);
                        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, lastLeftHandWeight);

                        //// 回転
                        //Quaternion handRotation = Quaternion.Euler(-90f, 180f, 0f);
                        //animator.SetIKRotation(AvatarIKGoal.LeftHand, handRotation);
                        //animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, lastLeftHandWait);

                        isLeftHandMoved = true;
                    }
                }

            }

            if (!isRightHandMoved)
            {
                // 右手を戻す
                lastRightHandWeight = Mathf.Lerp(lastRightHandWeight, 0.0f, 0.2f);
                animator.SetIKPosition(AvatarIKGoal.RightHand, cursorPosition);
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, lastRightHandWeight);
                //animator.SetIKRotationWeight(AvatarIKGoal.RightHand, lastRightHandWait);
            }

            if (!isLeftHandMoved)
            {
                // 左手を戻す
                lastLeftHandWeight = Mathf.Lerp(lastLeftHandWeight, 0.0f, 0.2f);
                animator.SetIKPosition(AvatarIKGoal.LeftHand, cursorPosition);
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, lastLeftHandWeight);
                //animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, lastLeftHandWait);

            }
        }


        #region UnityChan Candy Rock Star 用

        public void StartDance()
        {
            // 音楽は停止状態から
            if (refAudioSource)
            {
                refAudioSource.Pause();
                refAudioSource.time = 0;
            }
            if (animator) animator.SetBool("Dancing", true);
            //if (animator) animator.Play("003_NOT01_Final", 0, 0);
            //if (animator) animator.SetTrigger("StartDancing");
        }

        public void StopDance()
        {
            if (refAudioSource) refAudioSource.Pause();
            if (animator) animator.SetBool("Dancing", false);
            //if (animator) animator.Play("IK_HAND", 0, 0);
            //if (animator) animator.SetTrigger("StopDancing");
        }

        // UnityChan.FaceUpdate の代わりに受け取るイベントコール
        public void OnCallChangeFace(string str)
        {
            // 何もしない
        }

        // UnityChan.MusicStarter の代わりに受け取るイベントコール
        public void OnCallMusicPlay(string str)
        {
            switch (str)
            {
                // 文字列playを指定で再生開始
                case "play":
                    if (refAudioSource) refAudioSource.Play();
                    break;

                // 文字列stopを指定で再生停止
                case "stop":
                    if (refAudioSource) refAudioSource.Stop();
                    break;

                // それ以外はポーズ
                default:
                    if (refAudioSource) refAudioSource.Pause();
                    break;
            }
        }

        #endregion

    }
}