#import <AVFoundation/AVFoundation.h>

// The default Unity iOS audio session category (SoloAmbient) is muted by
// the hardware ringer/silent switch — music/SFX would silently stop
// whenever the player has silent mode on, which for a music-driven
// runner reads as "the game randomly has no sound". Playback category
// ignores the switch, matching how most music/game apps behave.
extern "C" {
    void _PlanetDash_UseAudioSessionPlaybackCategory()
    {
        NSError *error = nil;
        AVAudioSession *session = [AVAudioSession sharedInstance];
        [session setCategory:AVAudioSessionCategoryPlayback
                  withOptions:AVAudioSessionCategoryOptionMixWithOthers
                        error:&error];
        [session setActive:YES error:&error];
    }
}
