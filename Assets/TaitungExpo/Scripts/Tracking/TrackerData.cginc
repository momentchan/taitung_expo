#ifndef __TRACKER_DATA__
#define __TRACKER_DATA__
struct TrackerData
{
    bool active;
    bool isMoving;
    float2 pos;
    float2 dis;
    float2 dir;
    float lastUpdateTime;
    float activeRatio;
    float depth;
};
#endif