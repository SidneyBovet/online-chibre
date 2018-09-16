using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class MovePlayer : NetworkBehaviour
{
    [SerializeField]
    private Transform t;
    private const float delta = 0.0001f;

    void Update ()
	{
        if (!isLocalPlayer)
            return;
		if (Input.GetKey(KeyCode.W))
			t.Translate(new Vector3(0f,0f,delta) / Time.deltaTime);
		if (Input.GetKey(KeyCode.S))
			t.Translate(new Vector3(0f,0f,-delta) / Time.deltaTime);
		if (Input.GetKey(KeyCode.D))
			t.Translate(new Vector3(delta,0f,0f) / Time.deltaTime);
		if (Input.GetKey(KeyCode.A))
			t.Translate(new Vector3(-delta,0f,0f) / Time.deltaTime);
	}
}
