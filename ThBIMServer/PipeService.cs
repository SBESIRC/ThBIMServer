using System;
using System.IO;
using System.Linq;
using System.Text;
using System.IO.Pipes;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Security.Principal;
using System.Collections.Generic;
using ThBIMServer.Ifc2x3;

namespace ThBIMServer
{
    public class PipeService
    {
        public void Work()
        {
            Console.WriteLine("开启双通道监听...");
            Parallel.Invoke(() => PipeWorkFromCAD(), () => PipeWorkFromSU());
        }

        public void PipeWorkFromCAD()
        {
            // 获取管道数据
            var thProject = PipeConnect();

            if (null == thProject)
            {
                return;
            }

            Console.WriteLine("请选择ifc文件传输方式：1-文件模式 2-字节流模式");
            var option = Console.ReadLine();
            if (option == "1")
            {
                PipeWorkFromCADByFile(thProject);
            }
            else if (option == "2")
            {
                PipeWorkFromCADByStream(thProject);
            }
            else
            {
                Console.WriteLine("无效输入！");
            }
            Console.WriteLine("***************** CAD 通道已关闭 *******************");
            Console.WriteLine();
            Console.WriteLine();
            PipeWorkFromCAD();
        }

        public void PipeWorkFromCADByFile(ThTCHProjectData thProject)
        {
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
                using (var pipeClient = new NamedPipeClientStream(".", "THIFCFILE2P3DPIE", PipeDirection.Out, PipeOptions.None, TokenImpersonationLevel.Impersonation))
                {
                    var sw = new Stopwatch();
                    sw.Start();

                    pipeClient.Connect(5000);
                    var bytes = Encoding.UTF8.GetBytes(ifcFilePath);
                    pipeClient.Write(bytes, 0, bytes.Length);

                    sw.Stop();
                    Console.WriteLine("传输Ifc字节流，耗时 {0} 毫秒。", sw.ElapsedMilliseconds);
                    Console.WriteLine("");
                }
            }
        }

        public void PipeWorkFromCADByStream(ThTCHProjectData thProject)
        {
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

                        sw.Stop();
                        Console.WriteLine("成功生成Ifc模型，耗时 {0} 毫秒。", sw.ElapsedMilliseconds);
                        Console.WriteLine("");

                        using (var pipeClient = new NamedPipeClientStream(".",
                            "THIFCSTREAM2P3DIPE",
                            PipeDirection.Out,
                            PipeOptions.None,
                            TokenImpersonationLevel.Impersonation))
                        {
                            sw.Restart();
                            pipeClient.Connect(5000);
                            ThProtoBuf2IFC2x3Builder.SaveIfcModelByStream(model, pipeClient);
                            sw.Stop();
                            Console.WriteLine("传输Ifc字节流，耗时 {0} 毫秒。", sw.ElapsedMilliseconds);
                            Console.WriteLine("");
                        }
                    }
                }
                catch (Exception e)
                {
                    sw.Stop();
                    Console.WriteLine("无法保存数据：{0}", e.Message);
                }
            }
        }

        public ThTCHProjectData PipeConnect()
        {
            ThTCHProjectData thProject = null;
            using (var pipeServer = new NamedPipeServerStream("THCAD2SERVERPIPE", PipeDirection.In))
            {
                pipeServer.WaitForConnection();
                Console.WriteLine("***************** CAD 通道已开启 *******************");
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

            return thProject;
        }

        public void PipeWorkFromSU()
        {
            ThSUProjectData suProject = null;
            using (var suPipeServer = new NamedPipeServerStream("THSU2P3DPIPE", PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous))
            {
                suPipeServer.WaitForConnection();
                Console.WriteLine("***************** SU 通道已开启 *******************");

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
                fileName = suProject.Root.Name + ".ifc";
                ifcFilePath = Path.Combine(Path.GetTempPath(), fileName);
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
                try
                {
                    using (var pipeClient = new NamedPipeClientStream(".", "THIFCFILE2P3DPIE", PipeDirection.Out, PipeOptions.None, TokenImpersonationLevel.Impersonation))
                    {
                        pipeClient.Connect(5000);
                        var bytes = Encoding.UTF8.GetBytes(ifcFilePath);
                        pipeClient.Write(bytes, 0, bytes.Length);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("未正确连接到Viewer。");
                }
            }

            Console.WriteLine("***************** SU 通道已关闭 *******************");
            Console.WriteLine();
            Console.WriteLine();

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
