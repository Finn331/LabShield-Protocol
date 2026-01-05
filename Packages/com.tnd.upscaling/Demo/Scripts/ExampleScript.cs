using System.Collections.Generic;
using UnityEngine;

#if TND_BIRP || TND_URP || TND_HDRP
using TND.Upscaling.Framework;

public class ExampleScript : MonoBehaviour
{
    private TNDUpscaler _upscalerReference;

    void Start()
    {
        //Getting the list of currently supported Upscalers (NOTE, this is a static function so doesn’t need the _upscalerReference!
        List<UpscalerName> supportedUpscalers = TNDUpscaler.GetSupported();
        Debug.Log("Supported upscalers: " + string.Join(", ", supportedUpscalers));

        //Getting the reference to the TND Upscaler component
        _upscalerReference = GetComponent<TNDUpscaler>();

        //Setting the upscaling quality level to Balanced
        _upscalerReference.SetQuality(UpscalerQuality.Balanced);
    }
}
#endif
