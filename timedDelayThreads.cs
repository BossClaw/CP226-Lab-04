// Darryl LeCraw n01712877
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <pthread.h> // PTHREADS LIBRARY - GIVES US THREADS, CREATE/DETACH/JOIN

#ifdef _WIN32
#include <winsock2.h>
#include <ws2tcpip.h>
#define sleep(x) Sleep(1000 * (x))
#else
#include <sys/socket.h>
#include <netinet/in.h>
#include <unistd.h>
#endif

#define PORT 8080

// STRUCT TO BUNDLE CLIENT SOCKET + ID TOGETHER SO WE CAN PASS BOTH INTO A THREAD AS ONE POINTER
typedef struct
{
    SOCKET client_socket;
    int client_id;
} threaded_client_data;

// (ORIG) HANDLE CLIENT REQUEST - THE ACTUAL WORK DONE FOR EACH CLIENT
void handle_client(SOCKET client_socket, int client_id)
{
    char *message = "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 13\r\nConnection: close\r\n\r\nHello Client!";

    printf("[Server] Handling client %d...\n", client_id);

    // FAKE HEAVY WORK - BLOCKS THIS THREAD FOR 5 SECONDS (OTHER THREADS STILL RUN CONCURRENTLY)
    printf("[Server] Processing request...\n");
    sleep(5);

    send(client_socket, message, (int)strlen(message), 0); // SEND RESPONSE BACK TO CLIENT OVER THE SOCKET
    printf("[Server] Response sent to client %d. Closing connection.\n", client_id);

#ifdef _WIN32
    closesocket(client_socket); // CLOSE THE CLIENT SOCKET WHEN DONE - FREES OS RESOURCES
#else
    close(client_socket);
#endif
}

// THREAD ENTRY POINT - pthread_create CALLS THIS FUNCTION IN A NEW THREAD
// MUST TAKE void* AND RETURN void* - THAT'S THE REQUIRED PTHREAD SIGNATURE
void *client_thread(void *arg)
{
    threaded_client_data *data = (threaded_client_data *)arg; // CAST THE void* BACK TO OUR STRUCT SO WE CAN READ IT
    handle_client(data->client_socket, data->client_id);      // DO THE ACTUAL CLIENT WORK (BLOCKS HERE FOR 5s)
    free(data);                                               // FREE THE HEAP MEMORY WE ALLOCATED IN main() - THREAD OWNS THIS, SO THREAD FREES IT
    return NULL;                                              // THREAD EXITS CLEANLY
}

// START THE MAIN SHOW
int main()
{
#ifdef _WIN32
    WSADATA wsaData;
    WSAStartup(MAKEWORD(2, 2), &wsaData);
#endif

    SOCKET server_socket, client_socket;
    struct sockaddr_in server_addr, client_addr;
    int addr_len = sizeof(client_addr);
    int client_count = 0;

    server_socket = socket(AF_INET, SOCK_STREAM, 0);

    server_addr.sin_family = AF_INET;
    server_addr.sin_addr.s_addr = INADDR_ANY;
    server_addr.sin_port = htons(PORT);

    bind(server_socket, (struct sockaddr *)&server_addr, sizeof(server_addr));
    listen(server_socket, 5);

    printf("Server listening on port %d...\n", PORT);
    printf("NOTE: This server uses THREADS. It handles multiple clients concurrently!\n\nWoop woop!\n\n");

    // LOOP FOREVER - KEEP ACCEPTING NEW CLIENTS AS FAST AS THEY ARRIVE
    while (1)
    {
        // BLOCKS HERE UNTIL A CLIENT CONNECTS, THEN RETURNS A NEW SOCKET FOR THAT CLIENT
        client_socket = accept(server_socket, (struct sockaddr *)&client_addr, &addr_len);

        if (client_socket != INVALID_SOCKET)
        {
            client_count++;

            // THREAD ID - HANDLE TO THE NEW THREAD WE'RE ABOUT TO SPAWN
            pthread_t tid;

            // HEAP-ALLOCATE THE STRUCT SO IT SURVIVES AFTER THIS LOOP ITERATION ENDS
            // (STACK MEMORY WOULD GET OVERWRITTEN ON THE NEXT LOOP - DISASTER)
            threaded_client_data *data = malloc(sizeof(threaded_client_data));

            // IF MALLOC FAILED, CLOSE THE SOCKET AND SKIP - DON'T CRASH AND BURN
            if (!data) { closesocket(client_socket); continue; }

            // STORE THE SOCKET IN THE STRUCT
            data->client_socket = client_socket;
            // STORE THE ID IN THE STRUCT
            data->client_id = client_count;

            // SPAWN A NEW PTHREAD
            pthread_create(&tid, NULL, client_thread, data);

            // DETACH THE THREAD - THANKFULLY IT CLEANS ITSELF UP WHEN DONE
            pthread_detach(tid);
        }
    }

#ifdef _WIN32
    closesocket(server_socket);
    WSACleanup();
#else
    close(server_socket);
#endif
    return 0;
}
