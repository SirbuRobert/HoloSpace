// HoloSpace — SaveToGallery.mm
// Salvează un fișier video în Camera Roll folosind UIKit.
// UISaveVideoAtPathToSavedPhotosAlbum accesează Library/Caches fără restricții
// de sandbox, spre deosebire de PHPhotoLibrary care refuza acea cale (error 3302).
//
// Necesită în Info.plist (adăugate automat de XcodeBuildPostProcessor.cs):
//   NSPhotoLibraryUsageDescription
//   NSPhotoLibraryAddUsageDescription

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <Photos/Photos.h>
#import <objc/runtime.h>

// ─── Callback helper ─────────────────────────────────────────────────────────────

@interface HoloSpaceVideoSaver : NSObject
+ (instancetype)shared;
- (void)saveVideo:(NSString*)path;
@end

@implementation HoloSpaceVideoSaver

+ (instancetype)shared {
    static HoloSpaceVideoSaver* inst = nil;
    static dispatch_once_t t;
    dispatch_once(&t, ^{ inst = [HoloSpaceVideoSaver new]; });
    return inst;
}

- (void)saveVideo:(NSString*)path {
    if (![[NSFileManager defaultManager] fileExistsAtPath:path]) {
        NSLog(@"[HoloSpace] Fișierul video nu există: %@", path);
        return;
    }

    // Nu verificăm UIVideoAtPathIsCompatibleWithSavedPhotosAlbum — returnează NO
    // dacă fișierul e încă deschis/nefinalizat (moov atom neescris). Lăsăm iOS
    // să facă validarea intern la momentul salvării efective.
    UISaveVideoAtPathToSavedPhotosAlbum(
        path,
        self,
        @selector(video:didFinishSavingWithError:contextInfo:),
        NULL
    );
    NSLog(@"[HoloSpace] Salvare video inițiată: %@", path);
}

- (void)video:(NSString*)videoPath
    didFinishSavingWithError:(NSError*)error
             contextInfo:(void*)contextInfo {

    if (error) {
        NSLog(@"[HoloSpace] Eroare la salvarea în galerie: %@",
              error.localizedDescription);
    } else {
        NSLog(@"[HoloSpace] Video salvat cu succes în galerie.");
        // Ștergem fișierul temp după confirmare
        [[NSFileManager defaultManager] removeItemAtPath:videoPath error:nil];
    }
}

@end

// ─── C export pentru Unity ────────────────────────────────────────────────────────

extern "C" {

void HoloSpace_SaveVideoToGallery(const char* filePath) {
    if (filePath == NULL) return;
    NSString* path = [NSString stringWithUTF8String:filePath];
    dispatch_async(dispatch_get_main_queue(), ^{
        [[HoloSpaceVideoSaver shared] saveVideo:path];
    });
}

} // extern "C"
