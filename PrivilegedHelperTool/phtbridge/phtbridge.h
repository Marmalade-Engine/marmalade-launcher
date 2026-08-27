#import <Foundation/Foundation.h>

typedef void (*PHTRegisterCallback)(int status, const char *message); // status: 0 = error, 1 = success, 2 = requires approval (poll again)
typedef void (*PHTErrorCallback)(int eventType, const char *message); // eventType: 1 = Interrupted, 2 = Invalidated
typedef void (*PHTGetUserCallback)(const char* result);

#ifdef __cplusplus
extern "C" {
#endif

void PHTRegisterService(PHTRegisterCallback callback);
void PHTUnregisterService(void);

void *PHTCreateConnection(PHTErrorCallback callback);
void PHTCloseConnection(void* context);

void PHTGetUser(void* context, PHTGetUserCallback callback);

#ifdef __cplusplus
}
#endif
