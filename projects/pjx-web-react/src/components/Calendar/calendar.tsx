import React, { FunctionComponent, useState, useEffect, useCallback } from 'react';
import FullCalendar, { EventClickArg, DatesSetArg } from '@fullcalendar/react';
import dayGridPlugin from '@fullcalendar/daygrid';
import timeGridPlugin from "@fullcalendar/timegrid"; 
import interactionPlugin, { DateClickArg } from '@fullcalendar/interaction';
import AuthService from '../../services/authService';
import CalendarService, { EventDeleteProps, EventApiProps, EventApiResult } from '../../services/calendarService';

import { createStyles, Theme, withStyles, WithStyles } from '@material-ui/core/styles';
import Button from '@material-ui/core/Button';
import TextField from '@material-ui/core/TextField';
import Checkbox from '@material-ui/core/Checkbox';
import FormControlLabel from '@material-ui/core/FormControlLabel';
import Dialog from '@material-ui/core/Dialog';
import DialogActions from '@material-ui/core/DialogActions';
import DialogContent from '@material-ui/core/DialogContent';
import DialogContentText from '@material-ui/core/DialogContentText';
import DialogTitle from '@material-ui/core/DialogTitle';

const styles = (theme: Theme) =>
  createStyles({
    textField: {
      paddingTop: theme.spacing(2),
      paddingBottom: theme.spacing(2),
    },
    timeField: {
        paddingTop: theme.spacing(2),
        paddingBottom: theme.spacing(2),
        width: '50%'
    }
  });

type EventProps = {
    id?: string,
    title: string,
    start: string,
    end?: string,
    slotDuration?: string,
    allDay?: boolean
};

type DialogProps = {
    id?: string
    title: string,
    start?: string,
    startTime?: string,
    end?: string,
    allDay: boolean,
    action: string,
    deleteDisabled: boolean
}

const Calendar: FunctionComponent<WithStyles<typeof styles>> = (props) => {
    const fetcher = useCallback(() => {
        const authService = new AuthService();
        const calendarService = new CalendarService(authService);

        return calendarService;
      }, []);

    const { classes } = props;

    const [events, setEvents] = useState<EventProps[]>([]);
    const [open, setOpenDialog] = useState(false);
    const [dialogValues, setDialogValues] = useState<DialogProps>({ title: '', startTime: '00:00', allDay: true, action: 'Add', deleteDisabled: true });
    const [calView, setCalendarView] = useState({activeStart:'', activeEnd: ''});

    const handleOpenDialog = () => {
        setOpenDialog(true);
    };

    const handleCloseDialog = () => {
        setOpenDialog(false);
    };

    useEffect(() => {
        if (calView.activeStart && calView.activeEnd) {
            fetcher().readAllEvent({start: calView.activeStart, end: calView.activeEnd})
                .then((result:EventApiResult[]) => {
                    var events: EventProps[] = result.map((data:EventApiResult) => {
                        return { id: data.eventId.toString(), title: data.title, start: data.start, end: data.end, allDay: (data.end === null) };
                    });

                    setEvents(events);
                }).catch(error => {
                    console.log(error);
                })
        }
    }, [fetcher, calView]); 

    const handleDateClick = (arg: DateClickArg) => {
        setDialogValues(prevState => ({...prevState, action: 'Add', deleteDisabled: true, allDay: true, title: '', startTime: '00:00', start: arg.date.toISOString().substring(0, 10), end: '' }));
        handleOpenDialog();
    };

    const handleChange = (e:any) => {
        const { id, value } = e.target;

        setDialogValues(prevState => ({
            ...prevState,
            [id]: value
        }));
    };

    const handleAllDayChange = () => {
        setDialogValues(prevState => ({...prevState, end: '', startTime: '00:00', allDay: !dialogValues.allDay }));
    }

    const enableEnd = () => {
        if (dialogValues.allDay) {
            handleAllDayChange();
        }
    }

    const handleAddClick = () => {
        if (dialogValues.title.length && dialogValues.start) {
            var saveObj: EventApiProps = {
                id: dialogValues.id ? parseInt(dialogValues.id) : undefined,
                title: dialogValues.title, 
                start: `${dialogValues.start}T${dialogValues.startTime?.substring(0, 5)}:00-04:00`, // TODO: harding GMT-04 for now
                end: dialogValues.end
            };

            if (dialogValues.allDay){
                saveObj.end = undefined;
            }

            switch(dialogValues.action)
            {
                case "Add": 
                    fetcher().eventCreate(saveObj)
                    .then((result:EventApiResult) => {
                        setEvents([
                            ...events,
                            {
                                ...saveObj,
                                id: result.eventId.toString(),
                                allDay: dialogValues.allDay
                            }
                        ]);
                        
                        handleCloseDialog();
                    }).catch(error => {
                        console.log('calendarService.eventCreate(saveObj)');
                        console.log(`error: ${error}`);
                    });
                    break;
                case "Update":
                    fetcher().eventUpdate(saveObj)
                    .then((result:EventApiResult) => {
                        events[events.findIndex(value => { return value.id === result.eventId.toString(); })] = 
                            {
                                ...saveObj,
                                id: result.eventId.toString(),
                                allDay: dialogValues.allDay
                            };
                        setEvents([
                            ...events
                        ]);
                        
                        handleCloseDialog();
                    }).catch(error => {
                        console.log('calendarService.eventUpdate(saveObj)');
                        console.log(`error: ${error}`);
                    });
                    break;
            }
        }
    }

    const handleDeleteClick = () => {
        if (dialogValues.id) {
            var eventId: number = parseInt(dialogValues.id);
            var saveObj: EventDeleteProps = {
                eventId: eventId
            };

            fetcher().eventDelete(saveObj)
            .then((result:boolean) => {
                if (result) {
                    var deletedItem = events.splice(events.findIndex(value => { return value.id === eventId.toString(); }));
                    console.log('deletedItem');
                    console.log(deletedItem);

                    setEvents([
                        ...events
                    ]);
                    
                    handleCloseDialog();
                }
            }).catch(error => {
                console.log('calendarService.eventCreate(saveObj)');
                console.log(`error: ${error}`);
            });
        }
    }

    const handleEventClick = (arg:EventClickArg) => {
        setDialogValues(prevState => ({...prevState
            , id: arg.event.id
            , title: arg.event.title
            , end: (arg.event.end ? `${arg.event.end.toISOString().substring(0, 10)}T${arg.event.end.toLocaleTimeString()}` : '')
            , start: arg.event.start?.toISOString().substring(0, 10)
            , startTime: arg.event.start?.toLocaleTimeString()
            , allDay: arg.event.allDay
            , action: 'Update'
            , deleteDisabled: false
        }));
        handleOpenDialog();
    }

    const handleDatesSet = (arg:DatesSetArg) => {
        setCalendarView({activeStart: arg.view.activeStart.toISOString(), activeEnd: arg.view.activeEnd.toISOString()});
    }

    return (
        <>
            <FullCalendar
                themeSystem={'standard'}
                initialView={'dayGridMonth'}
                plugins={[ dayGridPlugin, timeGridPlugin, interactionPlugin ]}
                dateClick={handleDateClick}
                eventClick={handleEventClick}
                events={events}
                headerToolbar={{  
                    left: "prev,next",  
                    center: "title",  
                    right: "dayGridMonth,timeGridWeek,timeGridDay"  
                }}
                datesSet={handleDatesSet}
            />
            <Dialog open={open} onClose={handleCloseDialog} aria-labelledby="form-dialog-title">
                <DialogTitle id="form-dialog-title">Calendar Events</DialogTitle>
                <DialogContent>
                    <DialogContentText>{dialogValues.action} Event</DialogContentText>
                    <TextField
                        autoFocus
                        margin="normal"
                        className={classes.textField}
                        id="title"
                        label="Title"
                        type="string"
                        placeholder="enter title"
                        defaultValue={dialogValues.title}
                        onChange={handleChange}
                        required
                        fullWidth
                        variant="standard"
                    />
                    <FormControlLabel
                        control={
                            <Checkbox
                                id="allDay"
                                checked={dialogValues.allDay}
                                onChange={handleAllDayChange}
                                inputProps={{ 'aria-label': 'primary checkbox' }}
                            />
                        }
                        label="All day event"
                    />
                    <div>
                        <TextField
                            margin="normal"
                            className={classes.timeField}
                            id="start"
                            label="Start"
                            type='date' // or datetime-local
                            defaultValue={dialogValues.start}
                            onChange={handleChange}
                            required
                            variant="standard"
                        />
                        <TextField
                            margin="normal"
                            className={classes.timeField}
                            id="startTime"
                            label="Time"
                            type="time"
                            value={dialogValues.startTime}
                            onChange={handleChange}
                            required
                            fullWidth
                            variant="standard"
                            disabled={dialogValues.allDay}
                        />
                    </div>
                    <TextField
                        margin="normal"
                        className={classes.textField}
                        id="end"
                        label="End"
                        type="datetime-local"
                        defaultValue={dialogValues.end}
                        onChange={handleChange}
                        onClick={enableEnd}
                        fullWidth
                        variant="standard"
                        disabled={dialogValues.allDay}
                    />
                </DialogContent>
                <DialogActions>
                    <Button onClick={handleCloseDialog} color="primary">Cancel</Button>
                    <Button onClick={handleAddClick} color="primary">{dialogValues.action}</Button>
                    <Button onClick={handleDeleteClick} color="primary" disabled={dialogValues.deleteDisabled}>Delete</Button>
                </DialogActions>
            </Dialog>
        </>
    )
}

export default withStyles(styles)(Calendar);
