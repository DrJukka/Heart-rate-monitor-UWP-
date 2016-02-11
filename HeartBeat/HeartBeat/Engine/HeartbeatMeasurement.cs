/* Copyright (c) 2016 Microsoft Corporation. This software is licensed under the MIT License.
 * See the license file delivered with this project for further information.
 */
using System;

namespace HeartBeat.Engine
{
    public class HeartbeatMeasurement
    {
        public ushort HeartbeatValue { get; set; }
        public DateTimeOffset Timestamp { get; set; }

        public static HeartbeatMeasurement GetHeartbeatMeasurementFromData(ushort value, DateTimeOffset timeStamp)
        {
            return new HeartbeatMeasurement
            {
                HeartbeatValue = value,
                Timestamp = timeStamp
            };
        }
        public static HeartbeatMeasurement GetHeartbeatMeasurementFromData(byte[] data, DateTimeOffset timeStamp)
        {
            // Heart Rate profile defined flag values
            const byte HEART_RATE_VALUE_FORMAT = 0x01;
            byte flags = data[0];

            ushort HeartbeatMeasurementValue = 0;

            if (((flags & HEART_RATE_VALUE_FORMAT) != 0))
            {
                HeartbeatMeasurementValue = (ushort)((data[2] << 8) + data[1]);
            }
            else
            {
                HeartbeatMeasurementValue = data[1];
            }

            DateTimeOffset tmpVal = timeStamp;
            if (tmpVal == null)
            {
                tmpVal = DateTimeOffset.Now;
            }
            return new HeartbeatMeasurement
            {
                HeartbeatValue = HeartbeatMeasurementValue,
                Timestamp = tmpVal
            };
        }
    }
}
