import restify, { plugins, Request, Response, Next } from 'restify';
import routesByMethod from './routes';

var server = restify.createServer();

// plugins doc: http://restify.com/docs/plugins-api/#serverpre-plugins
server.pre(restify.plugins.pre.context()); //  creates req.set(key, val) and req.get(key) methods
server.pre(restify.plugins.pre.sanitizePath()); // Cleans up sloppy URLs on the request object, like /foo////bar/// to /foo/bar.

server.use(restify.plugins.acceptParser(server.acceptable)); // by default, server.acceptable = [ 'application/json',  'text/plain',  'application/octet-stream',  'application/javascript' ]
server.use(restify.plugins.authorizationParser()); // Parses out the Authorization header 
server.use(restify.plugins.dateParser());
server.use(restify.plugins.queryParser({ mapParams: false }));
server.use(restify.plugins.urlEncodedBodyParser());

server.get('/', function(req, res, next) {
  res.send('home')
  return next();
});

server.get('/healthcheck', (req, res) => {
  res.setHeader(
    'cache-control',
    'no-cache, no-store, max-age=0, must-revalidate'
  );
  console.log('healthcheck');
  res.send('success');
});

server.get('/crash', (req, res) => {
  throw new Error('Test error handling.'); 
});

// setup routes by methods
routesByMethod.get.forEach(route =>
  server.get(route.path, plugins.conditionalHandler(route.handlers))
);

server.listen(8081, function() {
  console.log('🚀  %s listening at %s', server.name, server.url);
});

process.on("uncaughtException", (options, error) => { 
  if (error && error.stack) console.log("EXCEPTION:", error.stack); 
  else console.log("EXCEPTION:", options); 
});

server.on('InternalServer', function(req, res, err, callback) {
  console.log('InternalServer'); 
  console.log(err.stack); 
  console.log(err.message);
  res.send(500, err);
});
  
server.on('restifyError', function(req, res, err, callback) {
  console.log('restifyError'); 
  console.log(err.stack); 
  console.log(err.message);
});

server.on('NotFound', function (req, res, err, callback) {
  console.log('Not found');
  res.send(404, err);
});
