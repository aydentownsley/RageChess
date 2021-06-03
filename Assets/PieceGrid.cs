using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PieceGrid : MonoBehaviour
{

    public float x_Start, y_Start, z_Start;

    public GameObject prefab;
    public float x_Space, z_Space;


    // Start is called before the first frame update
    void Start()
    {
        for (int i = 0; i < 8 * 8; i++)
        {
            Instantiate(prefab, new Vector3(x_Start + x_Space * (i % 8), y_Start + 0, z_Start + z_Space * (i / 8)), Quaternion.identity);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
