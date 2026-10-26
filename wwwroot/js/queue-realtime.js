/**
 * WAITWISE — Real-time SignalR Connection and Notification Engine
 */

const WaitWiseRealtime = (function () {
    let connection = null;

    // Web Audio API Synthesizer Chime (Ding-Dong) - Zero external audio file dependency
    function playChime() {
        try {
            const AudioContext = window.AudioContext || window.webkitAudioContext;
            if (!AudioContext) return;
            const ctx = new AudioContext();

            // Tone 1 (High note)
            const osc1 = ctx.createOscillator();
            const gain1 = ctx.createGain();
            osc1.type = 'sine';
            osc1.frequency.setValueAtTime(659.25, ctx.currentTime); // E5
            gain1.gain.setValueAtTime(0.2, ctx.currentTime);
            gain1.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.6);
            osc1.connect(gain1);
            gain1.connect(ctx.destination);
            osc1.start(ctx.currentTime);
            osc1.stop(ctx.currentTime + 0.6);

            // Tone 2 (Lower harmonic note)
            const osc2 = ctx.createOscillator();
            const gain2 = ctx.createGain();
            osc2.type = 'sine';
            osc2.frequency.setValueAtTime(523.25, ctx.currentTime + 0.25); // C5
            gain2.gain.setValueAtTime(0.25, ctx.currentTime + 0.25);
            gain2.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.9);
            osc2.connect(gain2);
            gain2.connect(ctx.destination);
            osc2.start(ctx.currentTime + 0.25);
            osc2.stop(ctx.currentTime + 0.9);
        } catch (e) {
            console.warn("Audio chime could not play automatically:", e);
        }
    }

    // Voice announcement using browser Speech Synthesis
    function announceTicket(ticketNumber, counterName) {
        if ('speechSynthesis' in window) {
            try {
                window.speechSynthesis.cancel(); // Stop any pending speech
                const text = `Ticket number ${ticketNumber}, please proceed to ${counterName}`;
                const utterance = new SpeechSynthesisUtterance(text);
                utterance.rate = 0.95;
                utterance.pitch = 1.0;
                utterance.lang = 'en-US';
                window.speechSynthesis.speak(utterance);
            } catch (err) {
                console.warn("Speech synthesis error:", err);
            }
        }
    }

    function getConnection() {
        if (!connection) {
            connection = new signalR.HubConnectionBuilder()
                .withUrl("/hubs/queue")
                .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
                .configureLogging(signalR.LogLevel.Information)
                .build();

            connection.onreconnecting((error) => {
                console.warn("SignalR reconnecting due to:", error);
                const statusEl = document.getElementById("signalr-connection-badge");
                if (statusEl) {
                    statusEl.className = "badge bg-warning text-dark";
                    statusEl.innerHTML = '<i class="bi bi-arrow-repeat spin"></i> Reconnecting...';
                }
            });

            connection.onreconnected((connectionId) => {
                console.log("SignalR reconnected:", connectionId);
                const statusEl = document.getElementById("signalr-connection-badge");
                if (statusEl) {
                    statusEl.className = "badge bg-success";
                    statusEl.innerHTML = '<i class="bi bi-broadcast"></i> Live';
                }
            });

            connection.onclose((error) => {
                console.error("SignalR connection closed:", error);
                const statusEl = document.getElementById("signalr-connection-badge");
                if (statusEl) {
                    statusEl.className = "badge bg-danger";
                    statusEl.innerHTML = '<i class="bi bi-wifi-off"></i> Disconnected';
                }
            });
        }
        return connection;
    }

    async function start() {
        const conn = getConnection();
        if (conn.state === signalR.HubConnectionState.Disconnected) {
            try {
                await conn.start();
                console.log("SignalR connected successfully.");
                const statusEl = document.getElementById("signalr-connection-badge");
                if (statusEl) {
                    statusEl.className = "badge bg-success";
                    statusEl.innerHTML = '<i class="bi bi-broadcast"></i> Live';
                }
            } catch (err) {
                console.error("SignalR start error:", err);
                setTimeout(start, 5000);
            }
        }
    }

    return {
        getConnection,
        start,
        playChime,
        announceTicket
    };
})();
