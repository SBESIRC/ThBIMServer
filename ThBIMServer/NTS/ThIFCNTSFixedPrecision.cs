using System;

namespace ThBIMServer.NTS
{
    public class ThIFCNTSFixedPrecision : IDisposable
    {
        private bool PrecisionReduce { get; set; }

        public ThIFCNTSFixedPrecision(bool precisionReduce = true)
        {
            PrecisionReduce = ThIFCNTSService.Instance.PrecisionReduce;
            ThIFCNTSService.Instance.PrecisionReduce = precisionReduce;
        }

        public void Dispose()
        {
            ThIFCNTSService.Instance.PrecisionReduce = PrecisionReduce;
        }
    }
}
