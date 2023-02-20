using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
public class DemoMenu : MonoBehaviour
{
    public TMP_Text Relatorio, Objetivo, Obtido, Pontuacao;
    public TMP_InputField InputExercicio;
    public Papete papete;
    public NucleoJogo nucleoJogo;
    public void CalibrarEJogar()
    {
        papete.AcionarColetaPadrao(); 
    }
    public void OnCalibracaoCompleta(bool resultado)
    {
        print("Calibracao completa com resultado " + resultado);
        if(resultado)
        {
            nucleoJogo.IniciarExercicio(InputExercicio.text, papete.PeEsquerdoEhMaisRecente());
        }
    }

    void Update()
    {
        Relatorio.text = nucleoJogo.ObterRelatorio();
        Relatorio.pageToDisplay = Relatorio.textInfo.pageCount;
        Objetivo.text = Papete.NomesExterno[(int)nucleoJogo.ObterPosicaoDesejadaAgora()];
        Obtido.text = Papete.NomesExterno[(int)nucleoJogo.ObterPosicaoJogador()];
        Pontuacao.text = nucleoJogo.ObterPontuacaoAtual().ToString();
    }
}
