using System;
using System.IO;
using System.Linq;
using System.Text;
using System.IO.Pipes;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Security.Principal;
using System.Collections.Generic;
using ThMEPIFC.Ifc2x3;

namespace ThBIMServer
{
    public class PipeService
    {
        public void Work()
        {
            Parallel.Invoke(() => PipeWorkFromCAD(), () => PipeWorkFromSU());
        }

        public void PipeWorkFromCAD()
        {
            // 获取管道数据
            ThTCHProjectData thProject = null;
            using (var pipeServer = new NamedPipeServerStream("THCAD2P3DPIPE", PipeDirection.In))
            {
                Console.WriteLine("等待CAD管道连接...");
                pipeServer.WaitForConnection();
                Console.WriteLine("CAD管道连接完成.");

                try
                {
                    thProject = new ThTCHProjectData();
                    byte[] PipeData = ReadPipeData(pipeServer);
                    if (VerifyPipeData(PipeData))
                    {
                        Google.Protobuf.MessageExtensions.MergeFrom(thProject, PipeData.Skip(10).ToArray());
                    }
                    else
                    {
                        throw new Exception("无法识别的CAD-Push数据!");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("无法获取管道数据：{0}", e.Message);
                }
            }

            //选择保存路径
            var time = DateTime.Now.ToString("HHmmss");
            var fileName = "模型数据" + time + ".ifc";
            var ifcFilePath = Path.Combine(Path.GetTempPath(), fileName);

            // 保存数据
            if (null != thProject)
            {
                var sw = new Stopwatch();
                sw.Start();
                try
                {
                    var model = ThProtoBuf2IFC2x3Factory.CreateAndInitModel("ThCAD2IFCProject", thProject.Root.GlobalId);
                    if (model != null)
                    {
                        ThProtoBuf2IFC2x3Builder.BuildIfcModel(model, thProject);
                        ThProtoBuf2IFC2x3Builder.SaveIfcModel(model, ifcFilePath);
                        model.Dispose();
                    }

                    sw.Stop();
                    Console.WriteLine("成功生成Ifc文件，耗时 {0} 毫秒。", sw.ElapsedMilliseconds);
                    Console.WriteLine("IFC文件路径：[{0}]", ifcFilePath);
                    Console.WriteLine("");
                }
                catch (Exception e)
                {
                    Console.WriteLine("无法保存数据：{0}", e.Message);
                }
                finally
                {
                    sw.Stop();
                }
            }

            // 发送文件
            if (File.Exists(ifcFilePath))
            {
                using (var pipeClient = new NamedPipeClientStream(".",
                    "THCAD2IFC2P3DPIPE", PipeDirection.Out, PipeOptions.None, TokenImpersonationLevel.Impersonation))
                {
                    pipeClient.Connect(5000);
                    var bytes = Encoding.UTF8.GetBytes(ifcFilePath);
                    pipeClient.Write(bytes, 0, bytes.Length);
                }
            }

            // 下一次连接
            PipeWorkFromCAD();
        }

        public void PipeWorkFromSU()
        {
            ThSUProjectData suProject = null;
            using (var suPipeServer = new NamedPipeServerStream("THSU2P3DPIPE", PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous))
            {
                Console.WriteLine("等待SU管道连接...");
                suPipeServer.WaitForConnection();
                Console.WriteLine("SU管道连接完成.");

                try
                {
                    suProject = new ThSUProjectData();
                    byte[] PipeData = ReadPipeData(suPipeServer);
                    if (VerifyPipeData(PipeData))
                    {
                        Google.Protobuf.MessageExtensions.MergeFrom(suProject, PipeData.Skip(10).ToArray());
                    }
                    else
                    {
                        throw new Exception("无法识别的SU-Push数据!");
                    }
                }
                catch (Exception e)
                {
                    suProject = null;
                    Console.WriteLine("无法识别的SU-Push数据：{0}", e.Message);
                }
            }

            // 选择保存路径
            var time = DateTime.Now.ToString("HHmmss");
            var fileName = "模型数据" + time + ".ifc";
            var ifcFilePath = Path.Combine(Path.GetTempPath(), fileName);

            // 保存数据
            if (null != suProject)
            {
                var sw = new Stopwatch();
                sw.Start();
                var Model = ThProtoBuf2IFC2x3Factory.CreateAndInitModel("ThSU2IFCProject", suProject.Root.GlobalId);
                if (Model != null)
                {
                    ThProtoBuf2IFC2x3Builder.BuildIfcModel(Model, suProject);
                    ThProtoBuf2IFC2x3Builder.SaveIfcModel(Model, ifcFilePath);
                    Model.Dispose();
                }
                sw.Stop();

                Console.WriteLine("成功生成Ifc文件，耗时 {0} 毫秒。", sw.ElapsedMilliseconds);
                Console.WriteLine("IFC文件路径：[{0}]", ifcFilePath);
                Console.WriteLine("");
            }

            // 发送文件
            if (File.Exists(ifcFilePath))
            {
                using (var pipeClient = new NamedPipeClientStream(".",
                    "THCAD2IFC2P3DPIPE",
                    PipeDirection.Out,
                    PipeOptions.None,
                    TokenImpersonationLevel.Impersonation))
                {
                    pipeClient.Connect(5000);
                    var bytes = Encoding.UTF8.GetBytes(ifcFilePath);
                    pipeClient.Write(bytes, 0, bytes.Length);
                }
            }

            PipeWorkFromSU();
        }

        /// <summary>
        /// 读取管道内数据
        /// </summary>
        private byte[] ReadPipeData(NamedPipeServerStream stream)
        {
            List<byte> result = new List<byte>();
            while (true)
            {
                byte[] bytes = new byte[256];
                var length = stream.Read(bytes, 0, bytes.Length);
                if (length == 256)
                    result.AddRange(bytes);
                else
                {
                    result.AddRange(bytes.Take(length));
                    break;
                }
            }
            return result.ToArray();
        }

        /// <summary>
        /// 验证管道数据规范性
        /// </summary>
        public bool VerifyPipeData(byte[] data)
        {
            return data[0] == 84 && data[1] == 72 //校验
                && (data[2] == 1 || data[2] == 2 || data[2] == 3) //push/zoom/外链
                && (data[3] == 1 || data[3] == 2); //CAD/SU
        }
    }
}
