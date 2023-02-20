using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
public class DemoPrevisao : MonoBehaviour
{
    public TMP_Text label;
    public Papete papete;
    void Update()
    {
        label.text = Papete.NomesExterno[(int)papete.ObterPosicao()];
    }
}
