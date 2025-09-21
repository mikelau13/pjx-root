import { transports, createLogger, format } from 'winston';

const level = process.env.LOG_LEVEL || 'debug';

const logger = createLogger({
  format: format.combine(
    format.timestamp({ format: 'MMMM DD, YYYY HH:mm:ss' }),
    format.splat(),
    format.json()
  ),
  transports: [
    new transports.Console({
      stderrLevels: ['error', 'critical', 'info', 'warn', 'debug','http','verbose','silly'],
    })
  ]
});

export default logger;
