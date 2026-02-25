using System;
using UnityEngine;

namespace GrowAGarden
{
    public class ARSController : MonoBehaviour
    {
        // Public 
        public event Action OnUpdate;

        // Serialized fields
        [Header("ARS Settings")]
        [Range(1.0f, 50.0f)][SerializeField] private float _arsRefreshRate = 50.0f; // FFR render rate in HZ
        [Range(0, 7)][SerializeField] private int _audioSourceChannel = 0;
        [Range(20, 20000)][SerializeField] float _fftMaxFreq = 16000f; // Max rendered frequency in HZ
        [Range(64, 4096)][SerializeField] int _fftSize = 1024; // FFT size, must be a power of 2
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] FFTWindow _fftWindowType = FFTWindow.Rectangular;
        [Range(0.0f, 100.0f)][SerializeField] private float _gainARSShaders = 1.0f;
        [Range(0.0f, 100.0f)][SerializeField] private float _gainChannel1 = 1.0f;
        [Range(0.0f, 100.0f)][SerializeField] private float _gainChannel2 = 1.0f;
        [Range(0.0f, 100.0f)][SerializeField] private float _gainChannel3 = 1.0f;
        [Range(0.0f, 100.0f)][SerializeField] private float _gainChannel4 = 1.0f;
        [Header("ARS Shader Texture")]
        [Tooltip("You can disable ARS Texture generation to save some resources")]
        [SerializeField] bool _enableARSTexture = true;
        [Tooltip("This is the render texture used in shaders")]
        [SerializeField] private CustomRenderTexture _arsRenderTexture;
        [Header("ARS Channels")]
        [Tooltip("You can disable ARS channels generation to save some resources")]
        [SerializeField] bool _enableARSChannels = true;
        [Tooltip("Min and max frequency of all channels, don't add or remove channels")]
        [SerializeField] Vector2 _channel1 = new(0.0f, 150.0f);
        [SerializeField] Vector2 _channel2 = new(150.0f, 500.0f);
        [SerializeField] Vector2 _channel3 = new(500.0f, 4000.0f);
        [SerializeField] Vector2 _channel4 = new(4000.0f, 16000.0f);
        [Header("Editor Debug")]
        [Tooltip("Debug texture, ")]
        [SerializeField] Texture _editorPreviewTexture;

        // Const fields
        private const string _arsTexture1Name = "ARS_Texture1";
        private const string _arsTexture1GainName = "ARS_Texture1Gain";
        private const string _arsFFTArray1Name = "ARS_FFTArray1";
        private const string _arsFFTArray1SizeName = "ARS_FFTArray1Size";
        private const string _channel1GainName = "ARS_Channel1Gain";
        private const string _channel2GainName = "ARS_Channel2Gain";
        private const string _channel3GainName = "ARS_Channel3Gain";
        private const string _channel4GainName = "ARS_Channel4Gain";

        // Private fields
        private float[] _fftRaw = null;
        private AudioSource _lastAudioSource = null;
        private AudioClip _lastAudioClip = null;
        private float _fftStep;
        private bool _forceRecreateFlag = false;
        private float[] _channels = new float[4];

        // Public properties
        public static ARSController Instance { get; private set; }
        public float[] Channels => _channels;
        public float LastUpdateTime { get; private set; }

        void Awake()
        {
            if (Instance == null)
                Init();
            else if (Instance != this)
            {
                Debug.LogError($"[{nameof(ARSController)}] Second instance detected, this is not allowed.");
                Destroy(gameObject);
            }
        }

        public static void SetAudioSource(AudioSource audioSource)
        {
            if (Instance._audioSource != audioSource)
                Instance._audioSource = audioSource;
        }

        public void SetARSTextureGain(float value)
        {
            _gainARSShaders = value;
            Shader.SetGlobalFloat(_arsTexture1GainName, value);
        }

        public void SetChannel1Gain(float value)
        {
            _gainChannel1 = value;
            Shader.SetGlobalFloat(_channel1GainName, value);
        }

        public void SetChannel2Gain(float value)
        {
            _gainChannel2 = value;
            Shader.SetGlobalFloat(_channel2GainName, value);
        }

        public void SetChannel3Gain(float value)
        {
            _gainChannel3 = value;
            Shader.SetGlobalFloat(_channel3GainName, value);
        }

        public void SetChannel4Gain(float value)
        {
            _gainChannel4 = value;
            Shader.SetGlobalFloat(_channel4GainName, value);
        }

        public static void ForceInit()
        {
            if (Instance == null)
            {
                ARSController instanceInScene = FindObjectOfType<ARSController>();
                if (instanceInScene != null)
                    instanceInScene.Init();
                else
                    Debug.LogError($"[{nameof(ARSController)}] No ARSController found in scene.");
            }
        }

        private void Init()
        {
            CheckFFTSize();
            _fftRaw = new float[_fftSize]; // Make sure fft size is a power of 2
            Instance = this;
            Shader.SetGlobalTexture(_arsTexture1Name, _arsRenderTexture);
            Shader.SetGlobalFloat(_arsTexture1GainName, _gainARSShaders);
            InvokeRepeating(nameof(SpectrumUpdate), 0.0f, 1.0f / _arsRefreshRate);
            Debug.Log($"[{nameof(ARSController)}] Init ARS - Refresh Rate: {_arsRefreshRate}, Audio Channel: {_audioSourceChannel}, Max frequency: {_fftMaxFreq}, Using FFT size: {_fftSize}");
        }

        private void SpectrumUpdate()
        {
            if (_audioSource == null || _fftRaw == null)
                return;

            AudioClip clip = _audioSource.clip;

            // Init new clip
            if (_lastAudioSource != _audioSource || _lastAudioClip != clip || _forceRecreateFlag)
            {
                _forceRecreateFlag = false;
                _lastAudioClip = clip;
                _lastAudioSource = _audioSource;
                int sampleRate = AudioSettings.outputSampleRate;
                _fftStep = (sampleRate * 0.5f) / _fftSize;
                int interestArraySize = (int)(_fftRaw.Length * (_fftMaxFreq / (sampleRate * 0.5f)));
                Shader.SetGlobalFloat(_arsFFTArray1SizeName, interestArraySize);

                Debug.Log($"[{nameof(ARSController)}] ARS New Audio Context : {_audioSource.name}, SampleRate: {sampleRate}, FftStep: {_fftStep}, InterestArraySize: {interestArraySize}");

                if (clip != null)
                    Debug.Log($"[{nameof(ARSController)}] Audio Clip - Name: {clip.name}, SamplesRate: {(int)(clip.samples / clip.length)}, Samples: {clip.samples}, Length: {clip.length}, channels: {clip.channels} ");
            }

            _audioSource.GetSpectrumData(_fftRaw, _audioSourceChannel, _fftWindowType);

            if (_enableARSTexture)
                UpdateShaderTexture();

            if (_enableARSChannels)
                UpdateARSChannels();

            LastUpdateTime = Time.time;
            OnUpdate?.Invoke();
        }

        private void UpdateShaderTexture()
        {
            Shader.SetGlobalFloatArray(_arsFFTArray1Name, _fftRaw);
            _arsRenderTexture.Update();
        }

        private void UpdateARSChannels()
        {
            Vector2[] channels = new Vector2[] { _channel1, _channel2, _channel3, _channel4 };

            // WARNING: Need to be very efficient
            float stepReciprocal = 1.0f / _fftStep;
            for (int i = 0; i < _channels.Length; i++)
            {
                Vector2 channelsSetting = channels[i];
                float fmin = channelsSetting.x;
                float fmax = channelsSetting.y;
                if (fmin < 0.0f || fmin > 20000.0f || fmin > fmax)
                    continue; // Config Error
                if (fmax < 0.0f || fmax > 20000.0f)
                    continue; // Config Error
                int stepStart = Math.Max((int)(fmin * stepReciprocal), 0);
                int stepEnd = Math.Min((int)(fmax * stepReciprocal), _fftRaw.Length);
                int count = stepEnd - stepStart;
                if (count == 0)
                    continue; // Config Error
                float amplitudeAccumulation = 0;
                for (int n = stepStart; n < stepEnd; n++)
                    amplitudeAccumulation += _fftRaw[n];
                float gain;
                if (i == 0)
                    gain = _gainChannel1;
                else if (i == 1)
                    gain = _gainChannel2;
                else if (i == 2)
                    gain = _gainChannel3;
                else
                    gain = _gainChannel4;
                _channels[i] = (amplitudeAccumulation / count) * gain;
            }
        }

        private void CheckFFTSize()
        {
            int size = (int)Mathf.Log(_fftSize, 2);
            size = (int)Mathf.Pow(2, size);
            _fftSize = Mathf.Clamp(size, 64, 4096);
        }

        [ContextMenu("- Reset ARS")]
        private void ForceReset()
        {
            if (!Application.isPlaying)
                return;
            Debug.Log($"[{nameof(ARSController)}] Reset ARS");
            CancelInvoke(nameof(SpectrumUpdate));
            Init();
            _forceRecreateFlag = true;
        }

        [ContextMenu("- Force Preview Texture")]
        private void OnValidate()
        {
            if (_editorPreviewTexture != null && !Application.isPlaying)
                Shader.SetGlobalTexture(_arsTexture1Name, _editorPreviewTexture);
            CheckFFTSize();
        }
    }
}
