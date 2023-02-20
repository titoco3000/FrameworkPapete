using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using UnityEngine;
using TMPro;
using UnityEngine.Events;


public class MenuCalibrador : MonoBehaviour
{
    public Animator anim;
    public TMP_Text[] legendas;
    public GameObject loadingScreen;
    public TMP_Text legendaLoadingScreen;

    private int[] quantidades = new int[] {0,0,0,0,0};
    private Stack<int> historicoMovimento = new();
    private Papete papete;
    public Action funcaoCancelar;
    private int dots = 0;
    private float lastDotTime = 0f;
    private float dotTimeInterval = 0.25f;
    private int direcaoDots = 1;

    private void Start()
    {
        papete = FindObjectOfType<Papete>();
    }
    private void ReescreverBotao(int id)
    {
        string seta = "·";
        switch (id)
        {
            case 0:
                seta = "↓";
                break;
            case 1:
                seta = "↑";
                break;
            case 3:
                seta = "→←";
                break;
            case 4:
                seta = "←→";
                break;
        }
        legendas[id].text = "<b>" + seta + "</b>\n" + Papete.NomesExterno[id] + "\n" + quantidades[id];
    }

    public void OnButtonHover(int id)
    {
        foreach (var nome in Papete.NomesInterno)
        {
            anim.SetBool(nome,false);
        }

        anim.SetBool(Papete.NomesInterno[id], true);
    }

    public void OnButtonClick(int id)
    {
        quantidades[id]++;
        
        historicoMovimento.Push(id);
        papete.SalvarDado((Papete.Posicao)id);
        ReescreverBotao(id);
    }

    public void CancelarCalibracao()
    {
        funcaoCancelar.Invoke();
    }
    public void BotaoCalibrar()
    {
        loadingScreen.SetActive(true);
        papete.Calibrar();
    }

    public void Desfazer()
    {
        if(historicoMovimento.Count > 0)
        {
            int id = historicoMovimento.Pop();
            if (quantidades[id] > 0)
                quantidades[id]--;
            ReescreverBotao(id);
            papete.DesfazerUltimaColeta();
        }
    }

    
    private void Update()
    {
        if(Time.time - lastDotTime > dotTimeInterval)
        {
            lastDotTime = Time.time;
            dots+=direcaoDots;
            if (dots > 4 || dots <= 0)
                direcaoDots = -direcaoDots;

            legendaLoadingScreen.text = "Calibrando";
            for (int i = 0; i < dots; i++)
                legendaLoadingScreen.text += ".";
        }
    }

}
