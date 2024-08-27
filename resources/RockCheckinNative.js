(function (global) {
    var promises = {};

    /*
     * Generate a UUID, or something close enough.
     */
    function uuidv4() {
        return ([1e7] + -1e3 + -4e3 + -8e3 + -1e11).replace(/[018]/g, c =>
            (c ^ crypto.getRandomValues(new Uint8Array(1))[0] & 15 >> c / 4).toString(16)
        );
    }

    global.RockCheckinNative = {
        ResolveNativePromise(promiseId, data, error) {
            if (error) {
                promises[promiseId].reject(data);
            }
            else {
                promises[promiseId].resolve(data);
            }

            delete promises[promiseId];
        },

        PrintLabels(tagJson) {
            return new Promise(function (resolve, reject) {
                var promiseId = uuidv4();
                promises[promiseId] = { resolve, reject };

                global.chrome.webview.postMessage({
                    eventName: "NATIVE",
                    eventData: JSON.stringify(["PrintLabels", promiseId, tagJson])
                });
            });
        },

        PrintV2Labels(tagJson) {
            return new Promise(function (resolve, reject) {
                var promiseId = uuidv4();
                promises[promiseId] = { resolve, reject };

                global.chrome.webview.postMessage({
                    eventName: "NATIVE",
                    eventData: JSON.stringify(["PrintV2Labels", promiseId, tagJson])
                });
            });
        }
    };
})(window);
