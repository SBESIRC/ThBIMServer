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
        private ThTCHProjectData thProject = null;
        private ThSUProjectData suProject = null;
        NamedPipeServerStream pipeServer = null;
        NamedPipeServerStream SU_pipeServer = null;

        public void Work()
        {
            Parallel.Invoke(() => PipeWorkFromCAD(), () => PipeWorkFromSU());
        }

        public void PipeWorkFromCAD()
        {
            thProject = null;
            pipeServer = new NamedPipeServerStream("THCAD2P3DPIPE", PipeDirection.In);
            pipeServer.WaitForConnection();

            Console.WriteLine("管道连接完成，正在生成Ifc文件。");

            // 获取数据
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
                thProject = null;
                Console.WriteLine("无法识别的CAD-Push数据：{0}", e.Message);
            }
            finally
            {
                pipeServer.Dispose();
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
                var Model = ThProtoBuf2IFC2x3Factory.CreateAndInitModel("ThCAD2IFCProject", thProject.Root.GlobalId);
                if (Model != null)
                {
                    ThProtoBuf2IFC2x3Builder.BuildIfcModel(Model, thProject);
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

            PipeWorkFromCAD();
        }

        public void PipeWorkFromSU()
        {
            suProject = null;
            SU_pipeServer = new NamedPipeServerStream("THSU2P3DPIPE", PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
            SU_pipeServer.WaitForConnection();

            Console.WriteLine("管道连接完成，正在生成Ifc文件。");

            try
            {
                suProject = new ThSUProjectData();
                byte[] PipeData = ReadPipeData(SU_pipeServer);
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
            finally
            {
                SU_pipeServer.Dispose();
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
            List<byte> _current = new List<byte>();
            while (true)
            {
                var i = stream.ReadByte();
                if (i == -1)
                {
                    return _current.ToArray();
                }
                _current.Add(Convert.ToByte(i));
            }
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
