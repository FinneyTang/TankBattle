using System.Collections;
using System.Collections.Generic;
using Main;
using UnityEngine;

public class XJXDefense : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        gameObject.GetComponent<Tank>().enabled = true;
    }
}
