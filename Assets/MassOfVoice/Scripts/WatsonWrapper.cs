
using IBM.Watson.DeveloperCloud.Logging;
using IBM.Watson.DeveloperCloud.Services.SpeechToText.v1;
using IBM.Watson.DeveloperCloud.Utilities;
using IBM.Watson.DeveloperCloud.DataTypes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MassOfVoice
{
    public class WatsonWrapper : MonoBehaviour
    {
        #region PLEASE SET THESE VARIABLES IN THE INSPECTOR
        [Space(10)]
        [Tooltip("The service URL (optional). This defaults to \"https://stream.watsonplatform.net/speech-to-text/api\"")]
        [SerializeField]
        string _serviceUrl = WatsonConsts.ServiceURL;
        [Header("CF Authentication")]
        [Tooltip("The authentication username.")]
        [SerializeField]
        string _username = WatsonConsts.Username;
        [Tooltip("The authentication password.")]
        [SerializeField]
        string _password = WatsonConsts.Password;
        [Header("IAM Authentication")]
        [Tooltip("The IAM apikey.")]
        [SerializeField]
        string _iamApikey = WatsonConsts.IAMAPIKey;
        [Tooltip("The IAM url used to authenticate the apikey (optional). This defaults to \"https://iam.bluemix.net/identity/token\".")]
        [SerializeField]
        string _iamUrl = WatsonConsts.IAMURL;
        [Header("TextObjectFactory")]
        [SerializeField] TextObjectFactory textObjectFactory;
        #endregion


        int _recordingRoutine;
        string _microphoneID;
        AudioClip _recording;
        int _recordingBufferSize = 1;
        int _recordingHZ = 22050;

        SpeechToText _service;

        void Start()
        {
            //LogSystem.InstallDefaultReactors();
            Runnable.Run(CreateService());
        }

        private IEnumerator CreateService()
        {
            //  Create credential and instantiate service
            Credentials credentials = null;
            if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
            {
                //  Authenticate using username and password
                credentials = new Credentials(_username, _password, _serviceUrl);
            }
            else if (!string.IsNullOrEmpty(_iamApikey))
            {
                //  Authenticate using iamApikey
                TokenOptions tokenOptions = new TokenOptions()
                {
                    IamApiKey = _iamApikey,
                    IamUrl = _iamUrl
                };

                credentials = new Credentials(tokenOptions, _serviceUrl);

                //  Wait for tokendata
                while (!credentials.HasIamTokenData())
                    yield return null;
            }
            else
            {
                throw new WatsonException("Please provide either username and password or IAM apikey to authenticate the service.");
            }

            _service = new SpeechToText(credentials);
            _service.RecognizeModel = "ja-JP_BroadbandModel";
            _service.StreamMultipart = true;

            Active = true;
            StartRecording();
        }

        public bool Active
        {
            get { return _service.IsListening; }
            set
            {
                if (value && !_service.IsListening)
                {
                    _service.DetectSilence = true;
                    _service.EnableWordConfidence = true;
                    _service.EnableTimestamps = true;
                    _service.SilenceThreshold = 0.01f;
                    _service.MaxAlternatives = 0;
                    _service.EnableInterimResults = true;
                    _service.OnError = OnError;
                    _service.InactivityTimeout = -1;
                    _service.ProfanityFilter = false;
                    _service.SmartFormatting = true;
                    _service.SpeakerLabels = false;
                    _service.WordAlternativesThreshold = null;
                    _service.StartListening(OnRecognize, OnRecognizeSpeaker);
                }
                else if (!value && _service.IsListening)
                {
                    _service.StopListening();
                }
            }
        }

        void StartRecording()
        {
            if (_recordingRoutine == 0)
            {
                UnityObjectUtil.StartDestroyQueue();
                _recordingRoutine = Runnable.Run(RecordingHandler());
            }
        }

        void StopRecording()
        {
            if (_recordingRoutine != 0)
            {
                Microphone.End(_microphoneID);
                Runnable.Stop(_recordingRoutine);
                _recordingRoutine = 0;
            }
        }

        void OnError(string error)
        {
            Active = false;

            Log.Debug("ExampleStreaming.OnError()", "Error! {0}", error);
        }

        IEnumerator RecordingHandler()
        {
            Log.Debug("ExampleStreaming.RecordingHandler()", "devices: {0}", Microphone.devices);
            _recording = Microphone.Start(_microphoneID, true, _recordingBufferSize, _recordingHZ);
            yield return null;      // let _recordingRoutine get set..

            if (_recording == null)
            {
                StopRecording();
                yield break;
            }

            bool bFirstBlock = true;
            int midPoint = _recording.samples / 2;
            float[] samples = null;

            while (_recordingRoutine != 0 && _recording != null)
            {
                int writePos = Microphone.GetPosition(_microphoneID);
                if (writePos > _recording.samples || !Microphone.IsRecording(_microphoneID))
                {
                    Log.Error("ExampleStreaming.RecordingHandler()", "Microphone disconnected.");

                    StopRecording();
                    yield break;
                }

                if ((bFirstBlock && writePos >= midPoint)
                  || (!bFirstBlock && writePos < midPoint))
                {
                    // front block is recorded, make a RecordClip and pass it onto our callback.
                    samples = new float[midPoint];
                    _recording.GetData(samples, bFirstBlock ? 0 : midPoint);

                    AudioData record = new AudioData();
                    record.MaxLevel = Mathf.Max(Mathf.Abs(Mathf.Min(samples)), Mathf.Max(samples));
                    record.Clip = AudioClip.Create("Recording", midPoint, _recording.channels, _recordingHZ, false);
                    record.Clip.SetData(samples, 0);

                    _service.OnListen(record);

                    bFirstBlock = !bFirstBlock;
                }
                else
                {
                    // calculate the number of samples remaining until we ready for a block of audio, 
                    // and wait that amount of time it will take to record.
                    int remaining = bFirstBlock ? (midPoint - writePos) : (_recording.samples - writePos);
                    float timeRemaining = (float)remaining / (float)_recordingHZ;

                    yield return new WaitForSeconds(timeRemaining);
                }

            }

            yield break;
        }

        void OnRecognize(SpeechRecognitionEvent result, Dictionary<string, object> customData)
        {
            if (result != null && result.results.Length > 0)
            {
                foreach (var res in result.results)
                {
                    foreach (var alt in res.alternatives)
                    {
                        string text = string.Format("{0} ({1}, {2:0.00})\n", alt.transcript, res.final ? "Final" : "Interim", alt.confidence);
                        Log.Debug("ExampleStreaming.OnRecognize()", text);
                        if (res.final)
                        {
                            StartCoroutine(textObjectFactory.CreateTextObjects(alt.transcript));
                            Debug.Log("WatsonWrapper: " + alt.transcript);
                        }
                    }

                    if (res.keywords_result != null && res.keywords_result.keyword != null)
                    {
                        foreach (var keyword in res.keywords_result.keyword)
                        {
                            Log.Debug("ExampleStreaming.OnRecognize()", "keyword: {0}, confidence: {1}, start time: {2}, end time: {3}", keyword.normalized_text, keyword.confidence, keyword.start_time, keyword.end_time);
                        }
                    }

                    if (res.word_alternatives != null)
                    {
                        foreach (var wordAlternative in res.word_alternatives)
                        {
                            Log.Debug("ExampleStreaming.OnRecognize()", "Word alternatives found. Start time: {0} | EndTime: {1}", wordAlternative.start_time, wordAlternative.end_time);
                            foreach (var alternative in wordAlternative.alternatives)
                                Log.Debug("ExampleStreaming.OnRecognize()", "\t word: {0} | confidence: {1}", alternative.word, alternative.confidence);
                        }
                    }
                }
            }
        }

        void OnRecognizeSpeaker(SpeakerRecognitionEvent result, Dictionary<string, object> customData)
        {
            if (result != null)
            {
                foreach (SpeakerLabelsResult labelResult in result.speaker_labels)
                {
                    Log.Debug("ExampleStreaming.OnRecognize()", string.Format("speaker result: {0} | confidence: {3} | from: {1} | to: {2}", labelResult.speaker, labelResult.from, labelResult.to, labelResult.confidence));
                }
            }
        }
    }
}