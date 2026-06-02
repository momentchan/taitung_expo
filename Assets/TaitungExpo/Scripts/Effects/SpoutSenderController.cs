using Klak.Spout;
using mj.gist;
using PrefsGUI;
using PrefsGUI.RapidGUI;
using UnityEngine;

namespace TaitungExpo
{
    /// <summary>
    /// Toggles Spout output: when on, renders <see cref="camera"/> to <see cref="outputTexture"/> and sends via <see cref="spoutSender"/>.
    /// </summary>
    public class SpoutSenderController : MonoBehaviour, IGUIUser
    {
        #region IGUIUser

        public string GetName() => "Spout";

        public void ShowGUI()
        {
            sendEnabled.DoGUI();
        }

        public void SetupGUI()
        {
            if (sendEnabled != null)
                return;

            sendEnabled = new PrefsBool($"{GetName()}_sendEnabled", false);
        }

        #endregion

        [SerializeField] private RenderTexture outputTexture;
        [SerializeField] private SpoutSender spoutSender;
        [SerializeField] private GameObject spoutCamera;

        private PrefsBool sendEnabled;
        private bool _lastSendEnabled;

        void Awake()
        {
            SetupGUI();
            ApplySendState(force: true);
        }

        void Update()
        {
            ApplySendState();
        }

        void OnDisable()
        {
            SetSendActive(false);
        }

        void ApplySendState(bool force = false)
        {
            bool on = sendEnabled != null && sendEnabled;
            if (!force && on == _lastSendEnabled)
                return;

            _lastSendEnabled = on;
            SetSendActive(on);
        }

        void SetSendActive(bool on)
        {
            if (spoutCamera != null)
                spoutCamera.SetActive(on);

            if (spoutSender == null)
                return;

            spoutSender.enabled = on;
            if (!on)
            {
                spoutSender.sourceTexture = null;
                return;
            }

            if (outputTexture == null)
                return;

            spoutSender.captureMethod = CaptureMethod.Texture;
            spoutSender.sourceTexture = outputTexture;
        }
    }
}
