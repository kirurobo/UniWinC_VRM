using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kirurobo {
    public class AnimationBase : MonoBehaviour
    {
        private AudioSource refAudioSource;

        // Start is called before the first frame update
        void Start()
        {
            // AudioSourceを取得
            if (!refAudioSource)
            {
                refAudioSource = FindAnyObjectByType<AudioSource>();
                refAudioSource.Pause();
            }
        }

        // Update is called once per frame
        void Update()
        {

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
    }
}
