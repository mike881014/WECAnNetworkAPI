using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Text;
using WECAnAPI;

abstract class PackParser
{

    int stat = 0;
    int dataLen = 0;
    int rDatalen = 0;
    List<byte[]> buffers = new List<byte[]>();

    public void Parse(byte[] chunk)
    {
        switch (stat)
        {
            case 0:
                {
                    for (int i = 0, I = chunk.Length; i < I; i++)
                    {
                        byte n = chunk[i];
                        dataLen = dataLen << 7 | n & 0x7f;
                        if ((n & 0x80) == 0)
                        {
                            if (dataLen != 0)
                                stat = 1;

                            if (i + 1 < I)
                            {
                                int copyLen = I - (i + 1);
                                byte[] nextChunk = new byte[copyLen];
                                Buffer.BlockCopy(chunk, i + 1, nextChunk, 0, copyLen);
                                Parse(nextChunk);
                            }

                            return;
                        }
                    }
                    break;
                }
            case 1:
                {
                    rDatalen += chunk.Length;
                    if (rDatalen < dataLen)
                    {
                        byte[] tmp = new byte[chunk.Length];
                        Buffer.BlockCopy(chunk, 0, tmp, 0, chunk.Length);
                        buffers.Add(tmp);
                        return;
                    }

                    byte[] next = null;
                    int remainLen = rDatalen - dataLen;
                    int remainId = chunk.Length - remainLen;
                    if (remainLen != 0)
                    {
                        int copyLen;
                        byte[] tmp;
                        copyLen = chunk.Length - remainId;
                        tmp = new byte[copyLen];
                        Buffer.BlockCopy(chunk, remainId, tmp, 0, copyLen);
                        next = tmp;
                        copyLen = remainId + 1;
                        tmp = new byte[copyLen];
                        Buffer.BlockCopy(chunk, 0, tmp, 0, copyLen);
                        chunk = tmp;
                    }

                    if (buffers.Count != 0)
                    {
                        buffers.Add(chunk);
                        int totalLen = 0;
                        byte[] tmp;
                        int ofs = 0;
                        foreach (var buffer in buffers)
                        {
                            totalLen += buffer.Length;
                        }

                        tmp = new byte[totalLen];

                        foreach (var buffer in buffers)
                        {
                            Buffer.BlockCopy(buffer, 0, tmp, ofs, buffer.Length);
                            ofs += buffer.Length;
                        }

                        chunk = tmp;
                    }

                    stat =
                    dataLen =
                    rDatalen = 0;
                    buffers.Clear();
                    Handle(chunk);
                    if (next != null)
                        Parse(next);

                    break;
                }
        }
    }

    public byte[] Pack(string str)
    {
        var data = Encoding.UTF8.GetBytes(str);
        int dataLen = data.Length;
        var header = new List<int>() { 0 };
        int headerIdx = 0;
        while (true)
        {
            header[headerIdx] |= dataLen & 0x7f;
            dataLen >>= 7;
            if (dataLen != 0)
            {
                header.Add(0x80);
                headerIdx++;
            }
            else
            {
                break;
            }
        }

        header.Reverse();
        dataLen = data.Length;
        byte[] tmp = new byte[header.Count + dataLen];
        int ofs = 0;
        foreach (byte h in header)
        {
            tmp[ofs] = h;
            ofs++;
        }

        Buffer.BlockCopy(data, 0, tmp, ofs, dataLen);
        return tmp;
    }

    public abstract void Handle(byte[] buffer);

}

class WECAnNetworkAPI : PackParser//我知道這大概是初始值之類的，但這個寫法是怎麼運作的
{

    static readonly string[] supportedOption = new string[] {
    "SimplifiedChinese",
    "GigaWordSimplified",
      // "UserWord",
      // "UnKnownWord",
      // "LogNotInUnKnownWord",
      // "AutoExpansionCDBU",
      // "JustUnique",
    "Idiom",
    "SimplePOSTransfer",
    "CRFSegment",
    "ChineseName",
    "ForceMergeNeuAndFW",
    "SeperateInSpace",
      // "ForceMergeWord",
      // "ForceSplitWord",
    "MixTwCn",
    "MergeConsecutiveSingleWord",
    "MergeVerbDirectionWord",
    "MergeSpecialThreeWord",
    "MergeSameWord",
    "SameWordSimplify",
    "MergeSpecialNeqaWord",
    "MergeO不O",
    "MergeSpecialNegativeWord",
      // "K",
      // "C",
      // "D",
      // "E",
      // "U",
      // "V",
  };
    static readonly bool SimplifiedChinese = false;
    static readonly bool GigaWordSimplified = false;
    // static readonly bool UserWord = false;
    // static readonly bool UnKnownWord = false;
    // static readonly bool LogNotInUnKnownWord = false;
    // static readonly bool AutoExpansionCDBU = false;
    // static readonly bool JustUnique = true;
    static readonly bool Idiom = false;
    static readonly bool SimplePOSTransfer = true;
    static readonly bool CRFSegment = false;
    static readonly bool ChineseName = false;
    static readonly bool ForceMergeNeuAndFW = true;
    static readonly bool SeperateInSpace = true;
    // static readonly bool ForceMergeWord = false;
    // static readonly bool ForceSplitWord = false;
    static readonly bool MixTwCn = false;
    static readonly bool MergeConsecutiveSingleWord = true;
    static readonly bool MergeVerbDirectionWord = true;
    static readonly bool MergeSpecialThreeWord = false;
    static readonly bool MergeSameWord = true;
    static readonly bool SameWordSimplify = false;
    static readonly bool MergeSpecialNeqaWord = true;
    static readonly bool MergeO不O = true;
    static readonly bool MergeSpecialNegativeWord = false;
    // static readonly int K = 2;
    // static readonly int C = 10;
    // static readonly int D = 60;
    // static readonly double E = 0.1;
    // static readonly double U = 1.2;
    // static readonly double V = 1.5;

    WECAnAPI.WECAnAPI api = new WECAnAPI.WECAnAPI(@"D:\專題\WECAn\新\WECAnCorpus");
    Socket currentClient = null;
    int bufferSize = 4096;

    public void StartServer(string host, int port)
    {
        byte[] buffer = new Byte[bufferSize];
        Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        listener.Bind(new IPEndPoint(IPAddress.Parse(host), port));
        listener.Listen(1);
        var serverInfo = (IPEndPoint)listener.LocalEndPoint;
        Console.WriteLine("# Listening ({0}:{1})", serverInfo.Address, serverInfo.Port);
        while (true)
        {
            try
            {
                Socket socket = listener.Accept();
                var clientInfo = (IPEndPoint)socket.RemoteEndPoint;
                currentClient = socket;
                Console.WriteLine("# Client ({0}:{1})", clientInfo.Address, clientInfo.Port);
                while (true)
                {
                    int recLen = socket.Receive(buffer);
                    byte[] chunk;
                    if (recLen == 0)
                    {
                        break;
                    }
                    else if (recLen == bufferSize)
                    {
                        chunk = buffer;
                    }
                    else
                    {
                        chunk = new byte[recLen];
                        Buffer.BlockCopy(buffer, 0, chunk, 0, recLen);
                    }
                    Parse(chunk);
                }

                currentClient = null;
                Console.WriteLine("# Client closed");
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }
            catch (Exception err)
            {
                Console.WriteLine("# Error {0}", err);
            }
        }
    }

    public override void Handle(byte[] buffer)
    {
        // Console.WriteLine("----len({0}) buffer({1})", buffer.Length, BitConverter.ToString(buffer));
        string str = Encoding.UTF8.GetString(buffer);
        int sep = str.IndexOf(':');
        string tid = str.Substring(0, sep);
        string text = str.Substring(sep + 1);
        currentClient.Send(Pack(String.Format("{0}:{1}", tid, WECAnSegement(text))));
    }

    string WECAnSegement(string text)
    {
        var seged = new List<string>();
        var posed = new List<string>();
        var config = new Dictionary<string, string>();
        var si = new StringReader(text);
        string returnData = null;
        for (int i = supportedOption.Length; i > 0; i--)
        {
            string setting = si.ReadLine();
            if (setting == null)
                return "Error. (3)";

            if (setting.Length == 0)
                continue;

            int sepOfs = setting.IndexOf('=');
            if (sepOfs == -1)
                return "Error. (4)";

            string key = setting.Substring(0, sepOfs);
            string val = setting.Substring(sepOfs + 1);
            config[key] = val;
        }

        string content = si.ReadToEnd();
        ConfigWECAn(config);
        try
        {
            api.Segment(content, seged, posed);
            for (int i = 0, l = seged.Count; i < l; i++)
            {
                returnData += seged[i];
                returnData += '(';
                returnData += posed[i];
                returnData += ')';
                returnData += '　';
            }

            return returnData;
        }
        catch (Exception)
        {
            // returnData = e.Message;
            return "Error. (2)";
        }
    }

    #region 設定值確認，與讀入
    void ConfigWECAn(Dictionary<string, string> config)
    {
        {
            if (config.ContainsKey("SimplifiedChinese"))
            {
                string val = config["SimplifiedChinese"];
                if (val == "true")
                    api.SetSimplifiedChinese(true);
                else if (val == "false")
                    api.SetSimplifiedChinese(false);
                else
                    api.SetSimplifiedChinese(SimplifiedChinese);
            }
            else
            {
                api.SetSimplifiedChinese(SimplifiedChinese);
            }

            if (config.ContainsKey("GigaWordSimplified"))
            {
                string val = config["GigaWordSimplified"];
                if (val == "true")
                    api.SetGigaWordSimplified(true);
                else if (val == "false")
                    api.SetGigaWordSimplified(false);
                else
                    api.SetGigaWordSimplified(GigaWordSimplified);
            }
            else
            {
                api.SetGigaWordSimplified(GigaWordSimplified);
            }

            if (config.ContainsKey("Idiom"))
            {
                string val = config["Idiom"];
                if (val == "true")
                    api.SetIdiom(true);
                else if (val == "false")
                    api.SetIdiom(false);
                else
                    api.SetIdiom(Idiom);
            }
            else
            {
                api.SetIdiom(Idiom);
            }

            if (config.ContainsKey("SimplePOSTransfer"))
            {
                string val = config["SimplePOSTransfer"];
                if (val == "true")
                    api.SetSimplePOSTransfer(true);
                else if (val == "false")
                    api.SetSimplePOSTransfer(false);
                else
                    api.SetSimplePOSTransfer(SimplePOSTransfer);
            }
            else
            {
                api.SetSimplePOSTransfer(SimplePOSTransfer);
            }

            if (config.ContainsKey("CRFSegment"))
            {
                string val = config["CRFSegment"];
                if (val == "true")
                    api.SetCRFSegment(true);
                else if (val == "false")
                    api.SetCRFSegment(false);
                else
                    api.SetCRFSegment(CRFSegment);
            }
            else
            {
                api.SetCRFSegment(CRFSegment);
            }

            if (config.ContainsKey("ChineseName"))
            {
                string val = config["ChineseName"];
                if (val == "true")
                    api.SetChineseName(true);
                else if (val == "false")
                    api.SetChineseName(false);
                else
                    api.SetChineseName(ChineseName);
            }
            else
            {
                api.SetChineseName(ChineseName);
            }

            if (config.ContainsKey("ForceMergeNeuAndFW"))
            {
                string val = config["ForceMergeNeuAndFW"];
                if (val == "true")
                    api.SetForceMergeNeuAndFW(true);
                else if (val == "false")
                    api.SetForceMergeNeuAndFW(false);
                else
                    api.SetForceMergeNeuAndFW(ForceMergeNeuAndFW);
            }
            else
            {
                api.SetForceMergeNeuAndFW(ForceMergeNeuAndFW);
            }

            if (config.ContainsKey("SeperateInSpace"))
            {
                string val = config["SeperateInSpace"];
                if (val == "true")
                    api.SetSeperateInSpace(true);
                else if (val == "false")
                    api.SetSeperateInSpace(false);
                else
                    api.SetSeperateInSpace(SeperateInSpace);
            }
            else
            {
                api.SetSeperateInSpace(SeperateInSpace);
            }

            if (config.ContainsKey("MixTwCn"))
            {
                string val = config["MixTwCn"];
                if (val == "true")
                    api.SetMixTwCn(true);
                else if (val == "false")
                    api.SetMixTwCn(false);
                else
                    api.SetMixTwCn(MixTwCn);
            }
            else
            {
                api.SetMixTwCn(MixTwCn);
            }

            if (config.ContainsKey("MergeConsecutiveSingleWord"))
            {
                string val = config["MergeConsecutiveSingleWord"];
                if (val == "true")
                    api.SetMergeConsecutiveSingleWord(true);
                else if (val == "false")
                    api.SetMergeConsecutiveSingleWord(false);
                else
                    api.SetMergeConsecutiveSingleWord(MergeConsecutiveSingleWord);
            }
            else
            {
                api.SetMergeConsecutiveSingleWord(MergeConsecutiveSingleWord);
            }

            if (config.ContainsKey("MergeVerbDirectionWord"))
            {
                string val = config["MergeVerbDirectionWord"];
                if (val == "true")
                    api.SetMergeVerbDirectionWord(true);
                else if (val == "false")
                    api.SetMergeVerbDirectionWord(false);
                else
                    api.SetMergeVerbDirectionWord(MergeVerbDirectionWord);
            }
            else
            {
                api.SetMergeVerbDirectionWord(MergeVerbDirectionWord);
            }

            if (config.ContainsKey("MergeSpecialThreeWord"))
            {
                string val = config["MergeSpecialThreeWord"];
                if (val == "true")
                    api.SetMergeSpecialThreeWord(true);
                else if (val == "false")
                    api.SetMergeSpecialThreeWord(false);
                else
                    api.SetMergeSpecialThreeWord(MergeSpecialThreeWord);
            }
            else
            {
                api.SetMergeSpecialThreeWord(MergeSpecialThreeWord);
            }

            if (config.ContainsKey("MergeSameWord"))
            {
                string val = config["MergeSameWord"];
                if (val == "true")
                    api.SetMergeSameWord(true);
                else if (val == "false")
                    api.SetMergeSameWord(false);
                else
                    api.SetMergeSameWord(MergeSameWord);
            }
            else
            {
                api.SetMergeSameWord(MergeSameWord);
            }

            if (config.ContainsKey("SameWordSimplify"))
            {
                string val = config["SameWordSimplify"];
                if (val == "true")
                    api.SetSameWordSimplify(true);
                else if (val == "false")
                    api.SetSameWordSimplify(false);
                else
                    api.SetSameWordSimplify(SameWordSimplify);
            }
            else
            {
                api.SetSameWordSimplify(SameWordSimplify);
            }

            if (config.ContainsKey("MergeSpecialNeqaWord"))
            {
                string val = config["MergeSpecialNeqaWord"];
                if (val == "true")
                    api.SetMergeSpecialNeqaWord(true);
                else if (val == "false")
                    api.SetMergeSpecialNeqaWord(false);
                else
                    api.SetMergeSpecialNeqaWord(MergeSpecialNeqaWord);
            }
            else
            {
                api.SetMergeSpecialNeqaWord(MergeSpecialNeqaWord);
            }

            if (config.ContainsKey("MergeO不O"))
            {
                string val = config["MergeO不O"];
                if (val == "true")
                    api.SetMergeO不O(true);
                else if (val == "false")
                    api.SetMergeO不O(false);
                else
                    api.SetMergeO不O(MergeO不O);
            }
            else
            {
                api.SetMergeO不O(MergeO不O);
            }

            if (config.ContainsKey("MergeSpecialNegativeWord"))
            {
                string val = config["MergeSpecialNegativeWord"];
                if (val == "true")
                    api.SetMergeSpecialNegativeWord(true);
                else if (val == "false")
                    api.SetMergeSpecialNegativeWord(false);
                else
                    api.SetMergeSpecialNegativeWord(MergeSpecialNegativeWord);
            }
            else
            {
                api.SetMergeSpecialNegativeWord(MergeSpecialNegativeWord);
            }

            // api.SetK(config.ContainsKey("K") ? int.Parse(config["K"]) : K);
            // api.SetC(config.ContainsKey("C") ? int.Parse(config["C"]) : C);
            // api.SetD(config.ContainsKey("D") ? int.Parse(config["D"]) : D);

            // api.SetE(config.ContainsKey("E") ? double.Parse(config["E"]) : E);
            // api.SetU(config.ContainsKey("U") ? double.Parse(config["U"]) : U);
            // api.SetV(config.ContainsKey("V") ? double.Parse(config["V"]) : V);
        }
    }
}
#endregion 

public class App
{

    public static void Main(String[] args)
    {
        var networkAPI = new WECAnNetworkAPI();
        networkAPI.StartServer("127.0.0.1", 7777);
    }

}