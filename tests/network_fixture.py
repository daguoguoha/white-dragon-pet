"""Exercise the real HTTP client against loopback; never contact a model provider."""
import json
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import subprocess
import threading
import time

counts = {'redirect_target': 0, 'requests': 0}


class Fixture(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass

    def do_POST(self):
        counts['requests'] += 1
        payload = json.loads(self.rfile.read(int(self.headers['Content-Length'])))
        assert self.headers['Authorization'] == 'Bearer fake-test-key'
        assert payload['model'] == 'test-model'
        assert payload['messages'][0]['role'] == 'system'
        assert payload['messages'][-1]['role'] == 'user'
        if self.path.startswith('/redirect-target'):
            counts['redirect_target'] += 1
        if self.path.startswith('/redirect/'):
            self.send_response(307)
            self.send_header('Location', '/redirect-target/v1/chat/completions')
            self.end_headers()
            return
        if self.path.startswith('/slow/'):
            time.sleep(2)
        if self.path.startswith('/fail/'):
            self.send_response(401)
            self.end_headers()
            return
        data = json.dumps({'choices': [{'message': {'content': 'hello from fixture'}}]}).encode()
        if self.path.startswith('/bad/'):
            data = b'{"unexpected":true}'
        if self.path.startswith('/large/'):
            data = b'x' * (1024 * 1024 + 1)
        self.send_response(200)
        self.send_header('Content-Type', 'application/json')
        self.send_header('Content-Length', str(len(data)))
        self.end_headers()
        try:
            self.wfile.write(data)
        except (BrokenPipeError, ConnectionResetError, ConnectionAbortedError):
            pass


if __name__ == '__main__':
    with ThreadingHTTPServer(('127.0.0.1', 0), Fixture) as server:
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            result = subprocess.run(['powershell.exe', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File',
                                     str(Path(__file__).with_name('Network.Tests.ps1')),
                                     '-Server', f'http://127.0.0.1:{server.server_port}'], check=False)
            assert counts['redirect_target'] == 0, 'credentials must never follow a redirect'
            assert counts['requests'] >= 27
            raise SystemExit(result.returncode)
        finally:
            server.shutdown()
            thread.join(timeout=3)
