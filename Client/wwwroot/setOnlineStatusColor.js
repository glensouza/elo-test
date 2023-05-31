// https://code-maze.com/use-browser-functionalities-with-blazor-webassembly/

var jsFunctions = {};
jsFunctions.registerOnlineStatusHandler = function (dotNetObjRef) {
    function onlineStatusHandler() {
        dotNetObjRef.invokeMethodAsync("SetOnlineStatusColor", navigator.onLine);
    };
    onlineStatusHandler();
    window.addEventListener("online", onlineStatusHandler);
    window.addEventListener("offline", onlineStatusHandler);
}
