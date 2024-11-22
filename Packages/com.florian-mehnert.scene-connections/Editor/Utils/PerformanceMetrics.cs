using System;

namespace SceneConnections.Editor.Utils
{
    public class PerformanceMetrics
    {
        public int BatchNumber { get; set; }
        public int NodesInBatch { get; set; }

        public int EdgesInBatch { get; set; }
        public double BatchCreationTime { get; set; }
        public double BatchLayoutTime { get; set; }
        public double TotalBatchTime => BatchCreationTime + BatchLayoutTime;
        public DateTime Timestamp { get; set; }
    }
}