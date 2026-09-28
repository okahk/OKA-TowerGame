mergeInto(LibraryManager.library, {
    SetWebPageTitle: function (titlePtr) {
        var title = UTF8ToString(titlePtr);
        document.title = title;
    },
	SetExitHash: function() {
		window.location.hash = "#exit";
    },
	SetSubmitScoreToRainbowOneApp: function (newHashUrlPtr) {
		var newHashUrl = UTF8ToString(newHashUrlPtr);
		window.location.hash = newHashUrl;
	},
	GetDeviceType: function() {
        var ua = navigator.userAgent || navigator.vendor || window.opera;
        // Portable: iPad, iPhone, iPod, Android, generic tablet/phone
        if (/iPad|iPhone|iPod|Android|Mobile|Tablet/.test(ua) && !window.MSStream) {
            return 1; // Portable device
        }
        // Desktop: Windows or Mac
        if (/Windows NT/.test(ua) || /Macintosh/.test(ua)) {
            return 2; // Windows or MacBook
        }
        return 0; // Other
    },
    IsTouchDevice: function() {
        var hasTouchPoints = (navigator.maxTouchPoints || navigator.msMaxTouchPoints || 0) > 0;
        var hasCoarsePointer = window.matchMedia && window.matchMedia("(pointer: coarse)").matches;
        var ua = navigator.userAgent || navigator.vendor || window.opera || "";
        var hasMobileUserAgent = /iPad|iPhone|iPod|Android|Mobile|Tablet/i.test(ua);

        return hasTouchPoints || hasCoarsePointer || hasMobileUserAgent ? 1 : 0;
    }
});