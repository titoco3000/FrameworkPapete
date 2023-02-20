/*
 * Permite a comunicação do arduino com Unity, tanto em um sentido quanto no outro,
 * além da conexão automática entre eles. Não depende do tipo do arduino ou da porta 
 * pela qual ele se comunica - “COM3”, “COM5”, etc. 
 * 
 * 22/06/2022
 * por Tito Guidotti
 * 
 */
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using System.IO.Ports; //para isso funcionar, API compatibility level não pode ser subset
using System.Threading;

public class ComunicArduino:MonoBehaviour
{
    public bool conectado;
    public int baudRate = 115200;

    public UnityEvent<bool> eventoConexao = new UnityEvent<bool>();
    public UnityEvent<string> eventoSerial = new UnityEvent<string>();
    
    private Thread thread;
    private SerialPort porta;


    //objeto que indica qual thread está no controle das variaveis
    static readonly object cadeado = new object();

    //variaveis de comunicação da thread de recebimento para a principal
    private string valorRecebido = "";
    private string buffer = "";
    private bool valorDisponivel = false;
    private bool forcarFechamento  = false;

    /// <summary>
    /// Cria um novo ComunicArduino dinamicamente
    /// </summary>
    /// <param name="pai">Onde vai ser criado</param>
    /// <param name="eventoConexao">Função chamada quando o arduino é conectado/desconectado</param>
    /// <param name="eventoMensagemSerial">Função chamada quando o arduino envia mensagens seriais (Serial.println)</param>
    /// <param name="baudRate">velocidade de comunicação serial</param>
    /// <returns>A instância criada</returns>
    public static ComunicArduino CriarReceptor(GameObject pai, UnityAction<bool> eventoConexao, UnityAction<string> eventoMensagemSerial, int baudRate = 115200)
    {
        ComunicArduino serial = pai.AddComponent<ComunicArduino>();
        serial.eventoConexao.AddListener(eventoConexao);
        serial.eventoSerial.AddListener(eventoMensagemSerial);
        serial.baudRate = baudRate;
        return serial;
    }


    /// <summary>
    /// Retorna um vetor dos nomes das portas disponíveis no sistema
    /// </summary>
    /// <returns>Vetor dos nomes das portas disponíveis</returns>
    public string[] PortasDisponiveis()
    {
        return SerialPort.GetPortNames();
    }

    /// <summary>
    /// Tenta conectar na porta especificada
    /// </summary>
    /// <param name="nome">Nome da porta a ser conectada</param>
    /// <returns>Se conectou com sucesso ou não</returns>
    public bool ConectarPorta(string nome)
    {
        if (conectado)
            throw new System.Exception("Tentando conectar em mais de uma porta ao mesmo tempo");
        try
        {
            lock (cadeado)
            {
                porta = new SerialPort(nome, baudRate);
                porta.Open();
                thread = new Thread(new ThreadStart(ThreadObservadoraSerial));
                thread.Start();
                conectado = true;
                eventoConexao.Invoke(true);
            }
        }
        catch (System.Exception)
        {

        }
        return conectado;
    }

    /// <summary>
    /// Desconecta da porta atual
    /// </summary>
    /// <exception cref="System.Exception">Quando não está conectado, dá uma exceção</exception>
    public void DesconectarPortaAtual()
    {
        lock (cadeado){
            if (!conectado)
                throw new System.Exception("Tentando desconectar de uma porta inexistente");
            thread.Abort();
            porta.Close();
            conectado = false;
            eventoConexao.Invoke(false);
            forcarFechamento = false;
        }
    }

    /// <summary>
    /// Entra em um loop paralelo até conseguir conectar com um arduino.
    /// Não há garantia de funcionamento se mais de um for conectado, e outros dispositivos podem causar problemas também (teoricamente)
    /// </summary>
    public void ConectarComPrimeiraPortaDisponivel()
    {
        //apenas para o caso de já estar em execução
        StopCoroutine(ConectarComPrimeiroCasoCoroutine());
        StartCoroutine(ConectarComPrimeiroCasoCoroutine());
    }

    /// <summary>
    /// Envia uma mensagem serial para o arduino
    /// </summary>
    /// <param name="msg">A mensagem a ser enviada</param>
    /// <exception cref="System.Exception">Quando tenta enviar e não está conectado</exception>
    public void Enviar(string msg)
    {
        if (!conectado)
            throw new System.Exception("Não está conectado");
        lock (cadeado)
        {
            porta.WriteLine(msg);
        }
    }
    /// <summary>
    /// Coroutine que busca o primeiro arduino conectado
    /// </summary>
    private IEnumerator ConectarComPrimeiroCasoCoroutine()
    {
        while (!conectado)
        {
            string[] portasDisponiveis = PortasDisponiveis();
            if (portasDisponiveis.Length > 0)
            {
                ConectarPorta(portasDisponiveis[0]);
            }
            yield return null;
        }
    }

    /// <summary>
    /// Thread secundária, fica em loop tentado ler a porta conectada e invoca o evento especificado
    /// </summary>
    private void ThreadObservadoraSerial()
    {
        while (true)
        {
            try
            {
                buffer = porta.ReadLine();
                lock (cadeado)
                {
                    valorRecebido = buffer;
                    valorDisponivel = true;
                }
            }
            //esse erro acontece quando o arduino é desconectado, portanto o sistema deve fechar a porta
            catch (System.IO.IOException)
            {
                forcarFechamento  = true;
            }
            
        }
    }

    private void Update()
    {
        if (forcarFechamento )
        {
            DesconectarPortaAtual();
        }
        if (valorDisponivel)
        {
            lock (cadeado)
            {
                eventoSerial.Invoke(valorRecebido);
                valorDisponivel = false;
            }
        }
    }
    /// <summary>
    /// Libera os recursos reservados
    /// </summary>
    private void OnDestroy()
    {
        if (conectado)
        {
            thread.Abort();
            porta.Close();
        }
    }

    
}
