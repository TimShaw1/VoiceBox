using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;
using TimShaw.VoiceBox.STT;
using TimShaw.VoiceBox.TTS;
using TimShaw.VoiceBox.LLM;

public class APITester : MonoBehaviour
{

    [SerializeField]
    public bool testAzure = false;

    [SerializeField]
    public bool testElevenlabs = false;

    [SerializeField]
    public bool testChatgpt = false;

    TaskFactory t = new TaskFactory();
    CancellationTokenSource tokenSource = new CancellationTokenSource();

    // Start is called before the first frame update
    void Start()
    {
        if (testAzure)
        {
            AzureSTT.Init("G6Z3xV8VNTTB2M3n6qLvLDLi6sSkhhXHg59T9fsSnrufHRXg3rDCJQQJ99BFACBsN54XJ3w3AAAYACOGeYHU", "canadacentral", "en-CA");
            t.StartNew(() => AzureSTT.Main(tokenSource.Token), tokenSource.Token);   
        }

        if (testElevenlabs)
        {
            ElevenLabs.Init("sk_fd1e422979c6cb05d645432c832029429fdba5d326ae5788", "Se2Vw1WbHmGbBbyWTuu4", 0f);
        }
    }

    private void OnDestroy()
    {
        Debug.Log("Cancelling");
        tokenSource.Cancel(); 
        Debug.Log("Cancelled");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
